using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="UserDeletionService"/> (WU1 data layer). The highest-value
/// backfill test in this suite: the Restrict-vs-Cascade FK handling is exactly the Postgres-specific
/// invariant <c>canalave-conventions/testing.md</c> §"Integration tests run against real Postgres"
/// was designed to protect. Without these tests, the only assurance was two manual end-to-end runs
/// against throwaway fixture users (see <c>audit/Identity.md</c> WU1 verification note).
///
/// <b>What's tested:</b> the four "Restrict" FKs that <see cref="UserDeletionService"/> must manually
/// resolve before the user row can be deleted — <c>UserProfileComment.ProfileUserId</c>,
/// <c>Notification.SourceUserId</c>, <c>FollowedUser.FollowedUserId</c>, and
/// <c>Vouch.VouchedUserId</c> — plus the <c>UserStat</c> Cascade that the database handles, and
/// (owner ruling D11, WU-TptHardDelete) a poll surviving its owner with NULL <c>owner_id</c>, its
/// options and the other users' votes.
///
/// <b>What stays manual</b> (per testing.md "What stays manual"): auth-cookie claim baking,
/// <c>SecurityStampValidator</c> timing, and SignalR circuit teardown. This test exercises the pure
/// data path via DI-scoped <see cref="UserDeletionService"/> — no HTTP requests or auth cookies.
///
/// <b>Isolation:</b> each test seeds its own throwaway user with a Guid-suffixed username and
/// deletes it; the DataSeeder's "TestUser"/"AdminUser" rows are never touched.
/// </summary>
[Collection("Postgres")]
public class UserDeletionServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{

    // ── basic return-value contract ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteUserAsync_UnknownUserId_ReturnsFalse()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        UserDeletionService sut = scope.ServiceProvider.GetRequiredService<UserDeletionService>();

        bool result = await sut.DeleteUserAsync(userId: int.MaxValue);

        result.Should().BeFalse("a non-existent user id must return false without throwing");
    }

    [Fact]
    public async Task DeleteUserAsync_ExistingUser_ReturnsTrue()
    {
        int userId = await SeedUserAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        UserDeletionService sut = scope.ServiceProvider.GetRequiredService<UserDeletionService>();

        bool result = await sut.DeleteUserAsync(userId);

        result.Should().BeTrue();
    }

    // ── user + cascade ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUserAsync_RemovesUserRow()
    {
        int userId = await SeedUserAsync();

        await DeleteUserViaServiceAsync(userId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool exists = await db.Users.AnyAsync(u => u.Id == userId);
        exists.Should().BeFalse("the user row itself must be gone after deletion");
    }

    [Fact]
    public async Task DeleteUserAsync_CascadesIntoUserStat()
    {
        int userId = await SeedUserAsync();
        await SeedUserStatAsync(userId);

        await DeleteUserViaServiceAsync(userId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool statExists = await db.UserStats.AnyAsync(s => s.UserId == userId);
        statExists.Should().BeFalse("UserStat has an OnDelete Cascade — it must be gone when the user is gone");
    }

    // ── Restrict FK: FollowedUser.FollowedUserId ─────────────────────────────────

    [Fact]
    public async Task DeleteUserAsync_RemovesFollowedUserRows_WhereThisUserIsFollowed()
    {
        // Scenario: some other user (the "follower") follows the user-to-delete.
        int targetUserId = await SeedUserAsync();
        int followerUserId = await SeedUserAsync();

        await SeedFollowedUserAsync(followerId: followerUserId, followedId: targetUserId);

        // Deleting the target would fail with a FK violation if the service did NOT clean up
        // FollowedUser rows where FollowedUserId == targetUserId first.
        await DeleteUserViaServiceAsync(targetUserId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool rowExists = await db.FollowedUsers
            .AnyAsync(f => f.FollowedUserId == targetUserId);
        rowExists.Should().BeFalse("FollowedUser rows where this user is the followed party must be removed");
    }

    // ── Restrict FK: Vouch.VouchedUserId ─────────────────────────────────────────

    [Fact]
    public async Task DeleteUserAsync_RemovesVouchRows_WhereThisUserIsVouched()
    {
        int targetUserId = await SeedUserAsync();
        int voucherUserId = await SeedUserAsync();

        await SeedVouchAsync(vouchingUserId: voucherUserId, vouchedUserId: targetUserId);

        await DeleteUserViaServiceAsync(targetUserId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool rowExists = await db.Vouches
            .AnyAsync(v => v.VouchedUserId == targetUserId);
        rowExists.Should().BeFalse("Vouch rows where this user is the vouched party must be removed");
    }

    // ── Restrict FK: Notification.SourceUserId ────────────────────────────────────

    [Fact]
    public async Task DeleteUserAsync_NullsOutSourceUserId_OnNotificationsSentByThisUser()
    {
        int targetUserId = await SeedUserAsync();
        int recipientUserId = await SeedUserAsync();

        long notificationId = await SeedNotificationAsync(sourceUserId: targetUserId, recipientUserId: recipientUserId);

        await DeleteUserViaServiceAsync(targetUserId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int? sourceId = await db.Notifications
            .Where(n => n.NotificationId == notificationId)
            .Select(n => n.SourceUserId)
            .FirstOrDefaultAsync();

        sourceId.Should().BeNull(
            "Notification.SourceUserId must be set to NULL (not deleted) when the source user is deleted — " +
            "the recipient's notification history is preserved");
    }

    // ── Restrict FK: UserProfileComment.ProfileUserId ────────────────────────────

    [Fact]
    public async Task DeleteUserAsync_RemovesUserProfileComments_OnThisUsersProfile()
    {
        int targetUserId = await SeedUserAsync();
        int commenterUserId = await SeedUserAsync();

        long commentId = await SeedUserProfileCommentAsync(profileUserId: targetUserId, commenterUserId: commenterUserId);

        await DeleteUserViaServiceAsync(targetUserId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool commentExists = await db.BaseComments
            .OfType<UserProfileComment>()
            .AnyAsync(c => c.CommentId == commentId);
        commentExists.Should().BeFalse(
            "UserProfileComment rows whose ProfileUserId matches the deleted user must be removed");

        // The base row too, read with raw SQL (owner ruling D10 — TptDelete.ProfileWallCommentsAsync): an
        // EF query over BaseComments would throw while materializing an orphan rather than report it.
        (await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*)::int AS \"Value\" FROM base_comments WHERE comment_id = {commentId}")
            .SingleAsync())
            .Should().Be(0, "the profile-wall delete removes the base_comments row, not only the child row");
    }

    // ── Polls survive their owner anonymized (owner ruling D11) ──────────────────

    [Fact]
    public async Task DeleteUserAsync_PollOnTheirOwnPost_SurvivesAnonymized_WithOptionsAndOtherUsersVotes()
    {
        int ownerId = await SeedUserAsync("owner");
        int voterB  = await SeedUserAsync("voterB");
        int voterC  = await SeedUserAsync("voterC");
        int postId  = await SeedProfilePostAsync(ownerId);
        (int pollId, int[] optionIds) = await SeedPollAsync<BlogPostPoll>(ownerId, postId, voterB, voterC);

        await DeleteUserViaServiceAsync(ownerId);

        await AssertPollSurvivedAnonymizedAsync(pollId, optionIds, [voterB, voterC]);
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.BlogPosts.Where(b => b.BlogPostId == postId).Select(b => new { b.AuthorId }).SingleAsync())
            .AuthorId.Should().BeNull("the post survives too, its author anonymized");
    }

    [Fact]
    public async Task DeleteUserAsync_PollTheyOwnOnSomeoneElsesPost_SurvivesAnonymized_AndThePostIsUntouched()
    {
        // D11's literal test shape: the poll's owner and the post's author differ.
        int pollOwnerId = await SeedUserAsync("pollOwner");
        int postAuthor  = await SeedUserAsync("postAuthor");
        int voterC      = await SeedUserAsync("voterC");
        int postId      = await SeedProfilePostAsync(postAuthor);
        (int pollId, int[] optionIds) = await SeedPollAsync<BlogPostPoll>(pollOwnerId, postId, postAuthor, voterC);

        await DeleteUserViaServiceAsync(pollOwnerId);

        await AssertPollSurvivedAnonymizedAsync(pollId, optionIds, [postAuthor, voterC]);
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.BlogPosts.Where(b => b.BlogPostId == postId).Select(b => new { b.AuthorId }).SingleAsync())
            .AuthorId.Should().Be(postAuthor);
    }

    [Fact]
    public async Task DeleteUserAsync_ADeletedModeratorsSitePoll_Survives()
    {
        int modId  = await SeedUserAsync("mod");
        int voterB = await SeedUserAsync("voterB");
        int voterC = await SeedUserAsync("voterC");
        (int pollId, int[] optionIds) = await SeedPollAsync<SitePoll>(modId, blogPostId: null, voterB, voterC);

        await DeleteUserViaServiceAsync(modId);

        await AssertPollSurvivedAnonymizedAsync(pollId, optionIds, [voterB, voterC]);
    }

    private async Task AssertPollSurvivedAnonymizedAsync(int pollId, int[] optionIds, int[] voterIds)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var poll = await db.Polls.Where(p => p.PollId == pollId).Select(p => new { p.OwnerId }).SingleOrDefaultAsync();
        poll.Should().NotBeNull("a poll is content and survives its owner (D11) — it used to CASCADE away");
        poll!.OwnerId.Should().BeNull();
        (await db.PollOptions.CountAsync(o => optionIds.Contains(o.PollOptionId))).Should().Be(optionIds.Length);
        (await db.PollVotes.Where(v => optionIds.Contains(v.PollOptionId)).Select(v => v.UserId).ToListAsync())
            .Should().BeEquivalentTo(voterIds, "other users' votes are not collateral of the owner leaving");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private async Task SeedUserStatAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStats.Add(new UserStat { UserId = userId });
        await db.SaveChangesAsync();
    }

    private async Task SeedFollowedUserAsync(int followerId, int followedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.FollowedUsers.Add(new FollowedUser
        {
            UserId = followerId,
            FollowedUserId = followedId,
            DateFollowed = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedVouchAsync(int vouchingUserId, int vouchedUserId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Vouches.Add(new Vouch
        {
            VouchingUserId = vouchingUserId,
            VouchedUserId = vouchedUserId,
            DateVouched = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task<long> SeedNotificationAsync(int sourceUserId, int recipientUserId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Notification notification = new()
        {
            RecipientUserId = recipientUserId,
            SourceUserId = sourceUserId,
            NotificationTypeId = NotificationTypeEnum.SiteAnnouncement, // seeded in InitialSchema HasData
            RelatedEntityId = 0,
            IsRead = false,
            DateCreated = DateTime.UtcNow
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        return notification.NotificationId;
    }

    private async Task<long> SeedUserProfileCommentAsync(int profileUserId, int commenterUserId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // UserProfileComment is a TPT child of BaseComment — add it through BaseComments
        // but EF resolves it to the correct tables via OfType<UserProfileComment>().
        UserProfileComment comment = new()
        {
            CommentText = "Test comment for deletion regression",
            DatePosted = DateTime.UtcNow,
            UserId = commenterUserId,
            ProfileUserId = profileUserId
        };
        db.BaseComments.Add(comment);
        await db.SaveChangesAsync();

        return comment.CommentId;
    }

    private async Task<int> SeedProfilePostAsync(int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ProfileBlogPost post = new()
        {
            AuthorId = authorId, Title = "Post", Content = "<p>body</p>", Rating = Rating.E,
            IsPublished = true, DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow
        };
        db.ProfileBlogPosts.Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    /// <summary>A two-option poll owned by <paramref name="ownerId"/>, one vote per voter (FK parents:
    /// the owner and voter users; the blog post for a <see cref="BlogPostPoll"/>).</summary>
    private async Task<(int PollId, int[] OptionIds)> SeedPollAsync<TPoll>(
        int ownerId, int? blogPostId, int voterOne, int voterTwo) where TPoll : BasePoll, new()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        TPoll poll = new()
        {
            OwnerId = ownerId, PollName = "Favorite starter?", DateOpened = DateTime.UtcNow,
            ResultsVisibility = PollResultsVisibility.Always, AnonymityMode = PollAnonymityMode.Anonymous,
        };
        if (poll is BlogPostPoll blogPoll) blogPoll.BlogPostId = blogPostId!.Value;
        poll.PollOptions.Add(new PollOption { Text = "Turtwig", SortOrder = 0 });
        poll.PollOptions.Add(new PollOption { Text = "Piplup",  SortOrder = 1 });
        db.Polls.Add(poll);
        await db.SaveChangesAsync();

        int[] optionIds = [..poll.PollOptions.OrderBy(o => o.SortOrder).Select(o => o.PollOptionId)];
        db.PollVotes.Add(new PollVote { PollOptionId = optionIds[0], UserId = voterOne });
        db.PollVotes.Add(new PollVote { PollOptionId = optionIds[1], UserId = voterTwo });
        await db.SaveChangesAsync();
        return (poll.PollId, optionIds);
    }

    private async Task DeleteUserViaServiceAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        UserDeletionService sut = scope.ServiceProvider.GetRequiredService<UserDeletionService>();
        await sut.DeleteUserAsync(userId);
    }
}
