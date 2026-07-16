using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="NotificationCleanupSweeper"/> (Feature 57): read
/// notifications older than <c>RetentionPeriod</c> are deleted; unread rows and younger read
/// rows survive. The timer worker (<see cref="NotificationCleanupWorker"/>) is removed by
/// <see cref="TestAppFactory"/>; tests drive the sweep body directly (the
/// <c>SpotlightGoLiveSweeper</c> pattern).
///
/// Tier: <b>Integration</b> (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class NotificationCleanupTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _recipientId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _recipientId = await SeedUserAsync();
    }

    [Fact]
    public async Task SweepAsync_DeletesOnlyReadNotificationsOlderThanRetention()
    {
        DateTime now = DateTime.UtcNow;
        DateTime pastCutoff  = now - NotificationCleanupSweeper.RetentionPeriod - TimeSpan.FromDays(1);
        DateTime insideCutoff = now - NotificationCleanupSweeper.RetentionPeriod + TimeSpan.FromDays(1);

        long readOld    = await SeedNotificationAsync(isRead: true,  dateCreated: pastCutoff);
        long readYoung  = await SeedNotificationAsync(isRead: true,  dateCreated: insideCutoff);
        long unreadOld  = await SeedNotificationAsync(isRead: false, dateCreated: pastCutoff);
        long unreadYoung = await SeedNotificationAsync(isRead: false, dateCreated: insideCutoff);

        int deleted = await RunSweepAsync();

        deleted.Should().Be(1, "only the read row past the retention cutoff is eligible");

        long[] remaining = await GetRemainingNotificationIdsAsync();
        remaining.Should().NotContain(readOld, "read + older than retention must be deleted");
        remaining.Should().Contain(readYoung, "read but younger than retention is kept");
        remaining.Should().Contain(unreadOld, "unread rows are kept indefinitely regardless of age");
        remaining.Should().Contain(unreadYoung, "unread + young is kept");
    }

    [Fact]
    public async Task SweepAsync_NothingEligible_DeletesNothing()
    {
        await SeedNotificationAsync(isRead: false,
            dateCreated: DateTime.UtcNow - NotificationCleanupSweeper.RetentionPeriod - TimeSpan.FromDays(1));
        await SeedNotificationAsync(isRead: true, dateCreated: DateTime.UtcNow);

        int deleted = await RunSweepAsync();

        deleted.Should().Be(0, "no row is both read and older than the retention cutoff");
        (await GetRemainingNotificationIdsAsync()).Should().HaveCount(2);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a notification row directly (no semantic write method sets IsRead + an aged
    /// DateCreated). <c>RelatedEntityId = 0</c> — valid int, no FK on the polymorphic column;
    /// <c>SourceUserId = null</c> — no actor needed. FK parents: recipient from
    /// <c>SeedUserAsync</c>; <c>NotificationTypeId</c> from model HasData seed (survives Respawn).
    /// </summary>
    private async Task<long> SeedNotificationAsync(bool isRead, DateTime dateCreated)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Notification row = new()
        {
            RecipientUserId    = _recipientId,
            NotificationTypeId = NotificationTypeEnum.SiteAnnouncement,
            SourceUserId       = null,
            RelatedEntityId    = 0,
            IsRead             = isRead,
            DateCreated        = dateCreated
        };
        db.Notifications.Add(row);
        await db.SaveChangesAsync();
        return row.NotificationId;
    }

    private async Task<int> RunSweepAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<NotificationCleanupSweeper>().SweepAsync();
    }

    private async Task<long[]> GetRemainingNotificationIdsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.Select(n => n.NotificationId).ToArrayAsync();
    }
}
