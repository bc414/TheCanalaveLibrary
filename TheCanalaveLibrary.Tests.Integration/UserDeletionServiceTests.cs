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
/// <c>Vouch.VouchedUserId</c> — plus the <c>UserStat</c> Cascade that the database handles.
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

    private async Task DeleteUserViaServiceAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        UserDeletionService sut = scope.ServiceProvider.GetRequiredService<UserDeletionService>();
        await sut.DeleteUserAsync(userId);
    }
}
