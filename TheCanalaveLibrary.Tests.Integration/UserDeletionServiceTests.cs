using FluentAssertions;
using Microsoft.AspNetCore.Identity;
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
/// options and the other users' votes. Owner ruling D13's survival set (WU-UserDeletion): a voter's
/// votes survive anonymized and the poll read keeps its tally and voter-list length; the
/// <c>poll_votes</c> unique index's NULLS DISTINCT; a conversation survives one participant's deletion
/// and is deleted (its open message reports closed) with the last; a sole admin's group survives
/// admin-less; external logins and notification settings go.
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

    [Fact]
    public async Task DeleteUserAsync_WhileItsScopeTracksAProfileWallComment_Succeeds()
    {
        // The scope's write context still tracks a comment on the target's wall (one scope stands in
        // for one circuit). The wall goes by raw SQL, which never reaches the tracker, and EF refuses to
        // Remove the user while a tracked RESTRICT dependent remains — so the service clears the tracker
        // first (WU-TptHardDelete review fixes; layer2-services.md §"Hard deletes of content parents").
        int targetUserId = await SeedUserAsync();
        int commenterUserId = await SeedUserAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserProfileComment tracked = new()
        {
            CommentText = "Tracked in the same scope", DatePosted = DateTime.UtcNow,
            UserId = commenterUserId, ProfileUserId = targetUserId
        };
        db.BaseComments.Add(tracked);
        await db.SaveChangesAsync();

        bool deleted = await scope.ServiceProvider.GetRequiredService<UserDeletionService>().DeleteUserAsync(targetUserId);

        deleted.Should().BeTrue();
        (await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*)::int AS \"Value\" FROM base_comments WHERE comment_id = {tracked.CommentId}")
            .SingleAsync())
            .Should().Be(0);
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

    // ── Poll votes survive anonymized (owner ruling D13, WU-UserDeletion) ─────────

    [Fact]
    public async Task DeleteUserAsync_Voters_VotesSurviveAnonymized_TallyAndVoterListKeepTheirLength()
    {
        // FK parents: the author → their published profile post → a Public single-choice poll (owner =
        // author) → its two options; then three voters. V1 and V3 delete their accounts, V2 stays.
        int authorId = await SeedUserAsync("author");
        int v1 = await SeedUserAsync("v1");
        int v2 = await SeedUserAsync("v2");
        int v3 = await SeedUserAsync("v3");
        int postId = await SeedProfilePostAsync(authorId);
        (int pollId, int[] optionIds) = await SeedPublicPollAsync(authorId, postId);
        await SeedVoteAsync(optionIds[0], v1);
        await SeedVoteAsync(optionIds[0], v2);
        await SeedVoteAsync(optionIds[1], v3);

        await DeleteUserViaServiceAsync(v1);
        await DeleteUserViaServiceAsync(v3);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.PollVotes.Where(v => optionIds.Contains(v.PollOptionId)).Select(v => v.UserId).ToListAsync())
            .Should().BeEquivalentTo(new int?[] { v2, null, null },
                "a deleted voter's row survives with user_id NULL (D13) — it used to CASCADE away");
        (await db.PollVotes.AnyAsync(v => v.UserId == v1 || v.UserId == v3))
            .Should().BeFalse("the anonymized rows no longer point at the deleted accounts");

        SetActiveUser(v2);
        PollDto poll = (await scope.ServiceProvider.GetRequiredService<IPollReadService>().GetPollAsync(pollId))!;
        poll.Options[0].VoteCount.Should().Be(2, "the published tally is unchanged");
        poll.Options[1].VoteCount.Should().Be(1);
        poll.Options[0].PublicVoters.Should().HaveCount(2, "the voter list keeps the count's length");
        poll.Options[0].PublicVoters[0].UserId.Should().Be(v2, "live names come first");
        poll.Options[0].PublicVoters[1].Should().Be(new PollVoterDto(null, null), "then the anonymized vote");
        poll.Options[1].PublicVoters.Should().ContainSingle().Which.UserId.Should().BeNull();
        poll.TotalVoterCount.Should().Be(3,
            "one live voter plus one per anonymized row — a bare Distinct would fold both deleted voters into one");
        poll.ConfigLocked.Should().BeTrue();
        poll.ViewerVotedOptionIds.Should().Equal([optionIds[0]], "V2's own vote is unaffected");

        SetActiveUser(FakeActiveUserContext.Anonymous());
        PollDto asAnonymous = (await scope.ServiceProvider.GetRequiredService<IPollReadService>().GetPollAsync(pollId))!;
        asAnonymous.ViewerVotedOptionIds.Should().BeEmpty(
            "\"has voted\" compares user_id to the viewer and never matches an anonymized row");
    }

    [Fact]
    public async Task PollVotes_UniqueIndex_RejectsADuplicateLiveVote_AndAllowsManyAnonymizedRows()
    {
        // Guards the index's NULLS DISTINCT: a future "fix" to NULLS NOT DISTINCT would make the second
        // anonymized row fail, and dropping uniqueness would let the duplicate live vote in.
        int authorId = await SeedUserAsync("author");
        int voter = await SeedUserAsync("voter");
        (_, int[] optionIds) = await SeedPublicPollAsync(authorId, await SeedProfilePostAsync(authorId));
        int optionId = optionIds[0];

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO poll_votes (poll_option_id, user_id, is_anonymous) VALUES ({optionId}, {voter}, false)");

        Func<Task> duplicate = () => db.Database.ExecuteSqlAsync(
            $"INSERT INTO poll_votes (poll_option_id, user_id, is_anonymous) VALUES ({optionId}, {voter}, false)");
        (await duplicate.Should().ThrowAsync<Npgsql.PostgresException>())
            .Which.SqlState.Should().Be("23505");

        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO poll_votes (poll_option_id, user_id, is_anonymous) VALUES ({0}, NULL, false), ({0}, NULL, false)",
            optionId);
        (await db.PollVotes.CountAsync(v => v.PollOptionId == optionId && v.UserId == null)).Should().Be(2);
    }

    // ── Conversations (owner ruling D13 edge (a), WU-UserDeletion) ────────────────

    [Fact]
    public async Task DeleteUserAsync_OneOfTwoParticipants_ConversationSurvives_ReadingDeletedUser()
    {
        int a = await SeedUserAsync("a");
        int b = await SeedUserAsync("b");
        (int conversationId, long[] messageIds) = await SeedConversationAsync(a, b);
        long reportId = await SeedMessageReportAsync(messageIds[0], reporterId: b);

        await DeleteUserViaServiceAsync(a);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Conversations.AnyAsync(c => c.ConversationId == conversationId)).Should().BeTrue();
        (await db.ConversationParticipants.Where(p => p.ConversationId == conversationId).Select(p => p.UserId).ToListAsync())
            .Should().Equal([b], "only the leaver's participant row cascades");
        var messages = await db.PrivateMessages.Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.DateSent).Select(m => new { m.MessageId, m.SenderUserId }).ToListAsync();
        messages.Select(m => m.MessageId).Should().Equal(messageIds, "every message survives — it is B's received mail too");
        messages.Select(m => m.SenderUserId).Should().Equal([null, b, null]);
        (await db.Reports.Where(r => r.ReportId == reportId).Select(r => r.ReportStatusId).SingleAsync())
            .Should().Be(ReportStatusEnum.Open, "the reported message still exists and can still be acted on");

        SetActiveUser(b);
        IMessagingReadService messaging = scope.ServiceProvider.GetRequiredService<IMessagingReadService>();
        ConversationThreadDto thread = await messaging.GetConversationThreadAsync(conversationId, page: 1, pageSize: 20);
        thread.OtherParticipant.Username.Should().Be("[deleted user]");
        thread.Messages.Where(m => m.SenderUserId is null).Should().HaveCount(2)
            .And.OnlyContain(m => m.SenderUsername == "[deleted user]" && !m.IsOwnMessage);
        (await messaging.GetConversationsAsync()).Should().ContainSingle(c => c.ConversationId == conversationId)
            .Which.OtherParticipant.Username.Should().Be("[deleted user]");
    }

    [Fact]
    public async Task DeleteUserAsync_BothParticipants_ConversationAndMessagesGone_AndTheirOpenReportsClosed()
    {
        int a = await SeedUserAsync("a");
        int b = await SeedUserAsync("b");
        int bystander = await SeedUserAsync("bystander");
        int c = await SeedUserAsync("c");
        (int conversationId, long[] messageIds) = await SeedConversationAsync(a, b);
        (int otherConversationId, _) = await SeedConversationAsync(bystander, c); // unrelated, must survive
        long reportId = await SeedMessageReportAsync(messageIds[1], reporterId: bystander);

        await DeleteUserViaServiceAsync(a);
        await DeleteUserViaServiceAsync(b);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Conversations.AnyAsync(x => x.ConversationId == conversationId))
            .Should().BeFalse("a conversation with no participant left is deleted at the source");
        (await db.PrivateMessages.AnyAsync(m => m.ConversationId == conversationId))
            .Should().BeFalse("its messages cascade with it");
        (await db.Conversations.AnyAsync(x => x.ConversationId == otherConversationId)).Should().BeTrue();

        var report = await db.Reports.Where(r => r.ReportId == reportId)
            .Select(r => new { r.ReportStatusId, r.ModeratorUserId, r.ActionTaken, r.DateResolved }).SingleAsync();
        report.ReportStatusId.Should().Be(ReportStatusEnum.ResolvedNoAction,
            "the reported message is gone, so its report would otherwise be a queue zombie (D7)");
        report.ModeratorUserId.Should().BeNull();
        report.DateResolved.Should().NotBeNull();
        report.ActionTaken.Should().Contain("both participants");
    }

    // ── Groups (owner ruling D13 edge (b)) ───────────────────────────────────────

    [Fact]
    public async Task DeleteUserAsync_SoleGroupAdmin_GroupSurvivesWithNoAdmin()
    {
        int creator = await SeedUserAsync("creator");
        int member = await SeedUserAsync("member");
        SetActiveUser(creator);
        int groupId;
        using (IServiceScope scope = Factory.Services.CreateScope())
            groupId = await scope.ServiceProvider.GetRequiredService<IGroupWriteService>()
                .CreateGroupAsync(new CreateGroupDto { GroupName = $"Group {Guid.NewGuid():N}" });
        SetActiveUser(member);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IGroupWriteService>().JoinAsync(groupId);

        await DeleteUserViaServiceAsync(creator);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Groups.Where(g => g.GroupId == groupId).Select(g => new { g.CreatorId }).SingleAsync())
                .CreatorId.Should().BeNull("the group survives its founder, anonymized");
            var roles = await db.GroupMembers.Where(m => m.GroupId == groupId)
                .Select(m => new { m.UserId, m.Role }).ToListAsync();
            roles.Should().ContainSingle().Which.Should().Be(new { UserId = member, Role = GroupRole.Member },
                "the group is admin-less — a waiting state a moderator ends (D13 edge (b))");
        }
    }

    // ── Account-level data goes unconditionally (D13 GDPR (iii)) ─────────────────

    [Fact]
    public async Task DeleteUserAsync_DestroysExternalLoginsAndNotificationSettings()
    {
        int userId = await SeedUserAsync("leaver");
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            UserManager<User> users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            User user = (await users.FindByIdAsync(userId.ToString()))!;
            (await users.AddLoginAsync(user, new UserLoginInfo("Discord", $"discord-{userId}", "Discord")))
                .Succeeded.Should().BeTrue();
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.UserNotificationSettings.Add(new UserNotificationSetting
            {
                UserId = userId, NotificationTypeId = NotificationTypeEnum.NewFollowerOnYou, EmailEnabled = true,
            });
            await db.SaveChangesAsync();
        }

        await DeleteUserViaServiceAsync(userId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Database.SqlQuery<int>(
                    $"SELECT COUNT(*)::int AS \"Value\" FROM \"AspNetUserLogins\" WHERE user_id = {userId}")
                .SingleAsync()).Should().Be(0, "a linked sign-in provider is account-level data");
            (await db.UserNotificationSettings.AnyAsync(s => s.UserId == userId)).Should().BeFalse();
        }
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

    /// <summary>A Public, results-always, single-choice blog-post poll with two options (FK parents: the
    /// owner and their post).</summary>
    private async Task<(int PollId, int[] OptionIds)> SeedPublicPollAsync(int ownerId, int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        BlogPostPoll poll = new()
        {
            OwnerId = ownerId, BlogPostId = blogPostId, PollName = "Favorite region?",
            DateOpened = DateTime.UtcNow.AddDays(-1),
            ResultsVisibility = PollResultsVisibility.Always, AnonymityMode = PollAnonymityMode.Public,
        };
        poll.PollOptions.Add(new PollOption { Text = "Sinnoh", SortOrder = 0 });
        poll.PollOptions.Add(new PollOption { Text = "Johto", SortOrder = 1 });
        db.Polls.Add(poll);
        await db.SaveChangesAsync();
        return (poll.PollId, [..poll.PollOptions.OrderBy(o => o.SortOrder).Select(o => o.PollOptionId)]);
    }

    private async Task SeedVoteAsync(int pollOptionId, int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.PollVotes.Add(new PollVote { PollOptionId = pollOptionId, UserId = userId });
        await db.SaveChangesAsync();
    }

    /// <summary>A two-participant conversation with three messages, oldest first: A, B, A.</summary>
    private async Task<(int ConversationId, long[] MessageIds)> SeedConversationAsync(int a, int b)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateTime start = DateTime.UtcNow.AddHours(-1);
        Conversation conversation = new() { Subject = "Hello", DateCreated = start };
        conversation.ConversationParticipants.Add(new ConversationParticipant { UserId = a });
        conversation.ConversationParticipants.Add(new ConversationParticipant { UserId = b });
        PrivateMessage[] messages =
        [
            new() { SenderUserId = a, MessageText = "<p>one</p>", DateSent = start.AddMinutes(1) },
            new() { SenderUserId = b, MessageText = "<p>two</p>", DateSent = start.AddMinutes(2) },
            new() { SenderUserId = a, MessageText = "<p>three</p>", DateSent = start.AddMinutes(3) },
        ];
        foreach (PrivateMessage m in messages) conversation.PrivateMessages.Add(m);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();
        return (conversation.ConversationId, [..messages.Select(m => m.MessageId)]);
    }

    /// <summary>An open member report on a message (reason 1 "Other" is migration-seeded).</summary>
    private async Task<long> SeedMessageReportAsync(long messageId, int reporterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Report report = new()
        {
            ReportedEntityType = ReportedEntityType.Message, ReportedEntityId = messageId,
            ReportReasonId = 1, ReporterUserId = reporterId, ReportStatusId = ReportStatusEnum.Open,
            DateReported = DateTime.UtcNow,
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report.ReportId;
    }

    private async Task DeleteUserViaServiceAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        UserDeletionService sut = scope.ServiceProvider.GetRequiredService<UserDeletionService>();
        await sut.DeleteUserAsync(userId);
    }
}
