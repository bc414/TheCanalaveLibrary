using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="INotificationWriteService"/> and
/// <see cref="INotificationReadService"/> (WU22 — Features 41/42/43).
///
/// <para><b>What's tested:</b>
/// <list type="bullet">
///   <item>Generation — <c>NotifyNewFollowerAsync</c>/<c>NotifyNewVouchAsync</c> create correct rows.</item>
///   <item>Create-core invariants — drop-self (notifying oneself is a no-op) and dedup
///   (duplicate unread notification for the same type+source+related is skipped).</item>
///   <item>Read — <c>GetUnreadCountAsync</c>, <c>GetNotificationsAsync</c> (order, pagination).</item>
///   <item>Mutations — <c>MarkAsReadAsync</c> (single), <c>MarkAllAsReadAsync</c>.</item>
///   <item>Settings — <c>GetSettingsAsync</c> returns defaults; <c>SetSettingAsync</c> upserts and
///   deletes the sparse row when values return to default.</item>
///   <item>End-to-end — <c>FollowAsync</c> (through <c>ServerFollowingWriteService</c>) creates
///   a <c>NewFollowerOnYou</c> notification row for the target.</item>
/// </list>
/// </para>
///
/// Tier: <b>Integration</b> (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class NotificationServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _actorId;
    private int _recipientId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _actorId = await SeedUserAsync();
        _recipientId = await SeedUserAsync();
        SetActiveUser(_actorId);
    }

    // ── NotifyNewFollowerAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task NotifyNewFollowerAsync_CreatesNotificationForRecipient()
    {
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);

        Notification? row = await GetNotificationAsync(
            _recipientId, NotificationTypeEnum.NewFollowerOnYou, _actorId, _actorId);

        row.Should().NotBeNull("a NewFollowerOnYou notification must be created for the recipient");
        row!.IsRead.Should().BeFalse("newly created notifications are unread");
        row.SourceUserId.Should().Be(_actorId);
        row.RelatedEntityId.Should().Be(_actorId, "relatedEntityId is the follower's user id");
    }

    // ── NotifyNewVouchAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyNewVouchAsync_CreatesNotificationForRecipient()
    {
        await CallNotifyNewVouchAsync(_recipientId, _actorId);

        Notification? row = await GetNotificationAsync(
            _recipientId, NotificationTypeEnum.NewVouchOnYou, _actorId, _actorId);

        row.Should().NotBeNull("a NewVouchOnYou notification must be created for the recipient");
        row!.SourceUserId.Should().Be(_actorId);
        row.RelatedEntityId.Should().Be(_actorId, "relatedEntityId is the voucher's user id");
    }

    // ── Create-core: drop-self ────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyNewFollowerAsync_DropsSelf_WhenRecipientEqualsSource()
    {
        // Scenario: actor notifies themselves (drop-self invariant).
        await CallNotifyNewFollowerAsync(_actorId, _actorId);

        Notification? row = await GetNotificationAsync(
            _actorId, NotificationTypeEnum.NewFollowerOnYou, _actorId, _actorId);

        row.Should().BeNull("drop-self: a user is never notified of their own action");
    }

    // ── Create-core: dedup ────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyNewFollowerAsync_Dedup_SecondCallSkippedWhileFirstUnread()
    {
        // First notification — should create.
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);

        // Second call with the same type + source + related — should be skipped (unread exists).
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);

        int count = await CountNotificationsAsync(
            _recipientId, NotificationTypeEnum.NewFollowerOnYou, _actorId);

        count.Should().Be(1, "dedup: second notification for the same unread event is skipped");
    }

    [Fact]
    public async Task NotifyNewFollowerAsync_AfterMarkAsRead_NewNotificationIsCreated()
    {
        // First notification, then mark it read, then re-notify — should create a new row.
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);
        SetActiveUser(_recipientId);
        await CallMarkAllAsReadAsync();

        SetActiveUser(_actorId);
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);

        int count = await CountNotificationsAsync(
            _recipientId, NotificationTypeEnum.NewFollowerOnYou, _actorId);

        count.Should().Be(2,
            "dedup only skips when an *unread* notification exists; a re-notify after marking read must produce a new row");
    }

    // ── GetUnreadCountAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetUnreadCountAsync_ReflectsUnreadNotifications()
    {
        // Seed two notifications for the recipient by two different actors.
        int actorB = await SeedUserAsync("NS-B");
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);
        await CallNotifyNewFollowerAsync(_recipientId, actorB);

        SetActiveUser(_recipientId);
        int count = await CallGetUnreadCountAsync();

        count.Should().BeGreaterThanOrEqualTo(2,
            "at least the two just-seeded notifications must be counted as unread");
    }

    [Fact]
    public async Task GetUnreadCountAsync_Anonymous_ReturnsZero()
    {
        Factory.Services.GetRequiredService<FakeActiveUserContext>().UserId = null;
        Factory.Services.GetRequiredService<FakeActiveUserContext>().IsAuthenticated = false;

        int count = await CallGetUnreadCountAsync();
        count.Should().Be(0);
    }

    // ── GetNotificationsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationsAsync_ReturnsNewestFirst()
    {
        int actorB = await SeedUserAsync("NS-C");

        // Two notifications for the recipient. Pin their timestamps explicitly — back-to-back
        // writes can land on the same instant (Windows' ~15 ms clock tick), so asserting on
        // creation order requires setting DateCreated, not sleeping and hoping the clock advanced.
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);
        await CallNotifyNewVouchAsync(_recipientId, actorB);
        await SetNotificationDateCreatedAsync(
            _recipientId, NotificationTypeEnum.NewFollowerOnYou, _actorId, DateTime.UtcNow.AddMinutes(-10));
        await SetNotificationDateCreatedAsync(
            _recipientId, NotificationTypeEnum.NewVouchOnYou, actorB, DateTime.UtcNow.AddMinutes(-5));

        SetActiveUser(_recipientId);
        NotificationDto[] dtos = await CallGetNotificationsAsync(page: 1, pageSize: 10);

        // Both should be present; the vouch (created last) must appear first.
        dtos.Should().ContainSingle(n =>
            n.NotificationTypeId == NotificationTypeEnum.NewVouchOnYou &&
            n.SourceUserId == actorB);

        int vouchIndex = Array.FindIndex(dtos, n =>
            n.NotificationTypeId == NotificationTypeEnum.NewVouchOnYou);
        int followIndex = Array.FindIndex(dtos, n =>
            n.NotificationTypeId == NotificationTypeEnum.NewFollowerOnYou &&
            n.SourceUserId == _actorId);

        vouchIndex.Should().BeGreaterThanOrEqualTo(0, "the vouch notification must be present");
        followIndex.Should().BeGreaterThanOrEqualTo(0, "the follow notification must be present");
        vouchIndex.Should().BeLessThan(followIndex, "newest notification must appear first");
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsEffectiveCollapsed()
    {
        // NewFollowerOnYou has DefaultCollapsed = false in the seed data.
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);

        SetActiveUser(_recipientId);
        NotificationDto[] dtos = await CallGetNotificationsAsync(page: 1, pageSize: 10);

        NotificationDto? dto = dtos.FirstOrDefault(n =>
            n.NotificationTypeId == NotificationTypeEnum.NewFollowerOnYou);
        dto.Should().NotBeNull();
        dto!.Collapsed.Should().BeFalse("DefaultCollapsed for NewFollowerOnYou is false");
    }

    // ── MarkAsReadAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAsReadAsync_MarksOnlyTheRequestedNotification()
    {
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);

        SetActiveUser(_recipientId);
        NotificationDto[] dtos = await CallGetNotificationsAsync(page: 1, pageSize: 10);
        NotificationDto target = dtos.First(n =>
            n.NotificationTypeId == NotificationTypeEnum.NewFollowerOnYou &&
            n.SourceUserId == _actorId);

        await CallMarkAsReadAsync(target.NotificationId);

        int unread = await CallGetUnreadCountAsync();
        // The specific notification must now be read.
        bool stillUnread = await IsNotificationUnreadAsync(target.NotificationId);
        stillUnread.Should().BeFalse("MarkAsReadAsync must flip IsRead to true");
    }

    [Fact]
    public async Task MarkAsReadAsync_CannotMarkAnotherUsersNotification()
    {
        // Seed a notification for _recipientId, then have _actorId try to mark it read.
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);

        SetActiveUser(_recipientId);
        NotificationDto[] dtos = await CallGetNotificationsAsync(page: 1, pageSize: 10);
        long notificationId = dtos.First(n =>
            n.NotificationTypeId == NotificationTypeEnum.NewFollowerOnYou &&
            n.SourceUserId == _actorId).NotificationId;

        // Switch to actor and try to mark the recipient's notification read.
        SetActiveUser(_actorId);
        await CallMarkAsReadAsync(notificationId);

        // The notification must still be unread for the recipient.
        bool stillUnread = await IsNotificationUnreadAsync(notificationId);
        stillUnread.Should().BeTrue(
            "MarkAsReadAsync must be scoped to the current user — cannot mark another user's notification");
    }

    // ── MarkAllAsReadAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAllAsReadAsync_ClearsAllUnreadForCurrentUser()
    {
        int actorB = await SeedUserAsync("NS-D");
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);
        await CallNotifyNewVouchAsync(_recipientId, actorB);

        SetActiveUser(_recipientId);
        await CallMarkAllAsReadAsync();

        int count = await CallGetUnreadCountAsync();
        count.Should().Be(0, "MarkAllAsReadAsync must clear all unread notifications for the current user");
    }

    // ── Settings: GetSettingsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetSettingsAsync_ReturnsDefaults_WhenNoOverrideExists()
    {
        SetActiveUser(_recipientId);
        NotificationSettingDto[] settings = await CallGetSettingsAsync();

        settings.Should().NotBeEmpty("all seeded notification types must be returned");

        NotificationSettingDto? followerSetting = settings.FirstOrDefault(
            s => s.TypeId == NotificationTypeEnum.NewFollowerOnYou);
        followerSetting.Should().NotBeNull();
        followerSetting!.IsDefault.Should().BeTrue("no override row → IsDefault = true");
        followerSetting.EmailEnabled.Should().BeTrue(
            "NewFollowerOnYou has DefaultEmailEnabled = true in the seed data");
        followerSetting.Collapsed.Should().BeFalse(
            "NewFollowerOnYou has DefaultCollapsed = false in the seed data");
    }

    // ── Settings: SetSettingAsync ────────────────────────────────────────────────

    [Fact]
    public async Task SetSettingAsync_CreatesOverrideRow()
    {
        SetActiveUser(_recipientId);
        // NewFollowerOnYou defaults: EmailEnabled = true, Collapsed = false.
        // Override to: EmailEnabled = false, Collapsed = true.
        await CallSetSettingAsync(NotificationTypeEnum.NewFollowerOnYou, emailEnabled: false, collapsed: true);

        NotificationSettingDto[] settings = await CallGetSettingsAsync();
        NotificationSettingDto? followerSetting =
            settings.FirstOrDefault(s => s.TypeId == NotificationTypeEnum.NewFollowerOnYou);

        followerSetting.Should().NotBeNull();
        followerSetting!.EmailEnabled.Should().BeFalse("override must be applied");
        followerSetting.Collapsed.Should().BeTrue("override must be applied");
        followerSetting.IsDefault.Should().BeFalse("an override row now exists");
    }

    [Fact]
    public async Task SetSettingAsync_DeletesOverrideRow_WhenValuesMatchDefault()
    {
        SetActiveUser(_recipientId);
        // Create an override first.
        await CallSetSettingAsync(NotificationTypeEnum.NewFollowerOnYou, emailEnabled: false, collapsed: true);

        // Now set back to defaults (EmailEnabled = true, Collapsed = false).
        await CallSetSettingAsync(NotificationTypeEnum.NewFollowerOnYou, emailEnabled: true, collapsed: false);

        NotificationSettingDto[] settings = await CallGetSettingsAsync();
        NotificationSettingDto? followerSetting =
            settings.FirstOrDefault(s => s.TypeId == NotificationTypeEnum.NewFollowerOnYou);

        followerSetting.Should().NotBeNull();
        followerSetting!.IsDefault.Should().BeTrue(
            "sparse model: setting both values back to defaults must delete the override row");
    }

    // ── WU33: GetTotalCountAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetTotalCountAsync_ReturnsCountOfAllNotificationsForCurrentUser()
    {
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);
        await CallNotifyNewVouchAsync(_recipientId, _actorId);

        SetActiveUser(_recipientId);
        int total = await CallGetTotalCountAsync();
        total.Should().Be(2, "exactly two notifications were seeded for the recipient");
    }

    [Fact]
    public async Task GetTotalCountAsync_Anonymous_ReturnsZero()
    {
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);

        Factory.Services.GetRequiredService<FakeActiveUserContext>().UserId = null;
        Factory.Services.GetRequiredService<FakeActiveUserContext>().IsAuthenticated = false;

        int total = await CallGetTotalCountAsync();
        total.Should().Be(0, "anonymous callers receive 0 for total count");
    }

    // ── WU33: Two-pass enrichment — SourceUserName ──────────────────────────────

    [Fact]
    public async Task GetNotificationsAsync_ReturnsSourceUserName_WhenSourceUserExists()
    {
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);

        SetActiveUser(_recipientId);
        NotificationDto[] dtos = await CallGetNotificationsAsync(1, 10);

        NotificationDto? n = dtos.FirstOrDefault(d =>
            d.NotificationTypeId == NotificationTypeEnum.NewFollowerOnYou);
        n.Should().NotBeNull();
        n!.SourceUserName.Should().NotBeNullOrEmpty(
            "SourceUserName must be resolved via LEFT JOIN when the source user exists");
        n.SourceUserId.Should().Be(_actorId);
    }

    // ── WU33: Two-pass enrichment — TargetTitle/TargetUrl (User kind) ───────────

    [Fact]
    public async Task GetNotificationsAsync_ReturnsTargetUrl_ForUserKindNotification()
    {
        // NewFollowerOnYou: RelatedEntityId = followerUserId (User kind).
        // TargetUrl must be /user/{actorId}; TargetTitle must be the actor's username.
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);

        SetActiveUser(_recipientId);
        NotificationDto[] dtos = await CallGetNotificationsAsync(1, 10);

        NotificationDto? n = dtos.FirstOrDefault(d =>
            d.NotificationTypeId == NotificationTypeEnum.NewFollowerOnYou);
        n.Should().NotBeNull();
        n!.TargetTitle.Should().NotBeNullOrEmpty(
            "TargetTitle must resolve to the actor's username for User-kind notifications");
        n.TargetUrl.Should().Be($"/user/{_actorId}",
            "TargetUrl for User-kind must be /user/{relatedEntityId}");
    }

    // ── WU33: Two-pass enrichment — null target for no-entity type ──────────────

    [Fact]
    public async Task GetNotificationsAsync_ReturnsNullTarget_ForSiteAnnouncementType()
    {
        // SiteAnnouncement maps to RelatedEntityKind.None — no batch-load, both fields null.
        // Seed via DbContext since no semantic write method exists yet.
        await SeedAnnouncementNotificationAsync(_recipientId);

        SetActiveUser(_recipientId);
        NotificationDto[] dtos = await CallGetNotificationsAsync(1, 10);

        NotificationDto? n = dtos.FirstOrDefault(d =>
            d.NotificationTypeId == NotificationTypeEnum.SiteAnnouncement);
        n.Should().NotBeNull("the seeded announcement row must appear in the feed");
        n!.TargetTitle.Should().BeNull(
            "SiteAnnouncement has no navigable target entity (RelatedEntityKind.None)");
        n.TargetUrl.Should().BeNull();
    }

    // ── WU33: OldestUnreadFirst ordering ──────────────────────────────────────────

    [Fact]
    public async Task GetNotificationsAsync_OldestUnreadFirst_PutsUnreadBeforeRead()
    {
        int actorB = await SeedUserAsync("NS-E");

        // First (older) notification: follow; second (newer): vouch. Pin both timestamps
        // explicitly so the ordering reflects the sort, not the wall clock (Windows' ~15 ms tick
        // can leave back-to-back writes on the same instant).
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);
        await CallNotifyNewVouchAsync(_recipientId, actorB);
        await SetNotificationDateCreatedAsync(
            _recipientId, NotificationTypeEnum.NewFollowerOnYou, _actorId, DateTime.UtcNow.AddMinutes(-10));
        await SetNotificationDateCreatedAsync(
            _recipientId, NotificationTypeEnum.NewVouchOnYou, actorB, DateTime.UtcNow.AddMinutes(-5));

        // Mark the older follow notification as read.
        SetActiveUser(_recipientId);
        NotificationDto[] initial = await CallGetNotificationsAsync(1, 10);
        NotificationDto follow = initial.First(n =>
            n.NotificationTypeId == NotificationTypeEnum.NewFollowerOnYou);
        await CallMarkAsReadAsync(follow.NotificationId);

        // OldestUnreadFirst: the unread vouch (newer but unread) must precede the read follow (older but read).
        NotificationDto[] ordered =
            await CallGetNotificationsAsync(1, 10, NotificationFeedOrder.OldestUnreadFirst);

        int vouchIdx  = Array.FindIndex(ordered, n => n.NotificationTypeId == NotificationTypeEnum.NewVouchOnYou);
        int followIdx = Array.FindIndex(ordered, n => n.NotificationTypeId == NotificationTypeEnum.NewFollowerOnYou);

        vouchIdx.Should().BeGreaterThanOrEqualTo(0);
        followIdx.Should().BeGreaterThanOrEqualTo(0);
        vouchIdx.Should().BeLessThan(followIdx,
            "OldestUnreadFirst: unread notifications must appear before read ones regardless of creation time");
    }

    // ── End-to-end: FollowAsync → notification ───────────────────────────────────

    [Fact]
    public async Task FollowAsync_ThroughFollowingWriteService_CreatesNotificationForTarget()
    {
        // Call FollowAsync (wired seam from WU22) and verify the notification row appears.
        await CallFollowAsync(_recipientId);

        Notification? row = await GetNotificationAsync(
            _recipientId, NotificationTypeEnum.NewFollowerOnYou, _actorId, _actorId);

        row.Should().NotBeNull(
            "FollowAsync must create a NewFollowerOnYou notification for the followed user");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    // ── Service call wrappers ─────────────────────────────────────────────────────

    private async Task CallNotifyNewFollowerAsync(int recipientId, int followerId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.NotifyNewFollowerAsync(recipientId, followerId);
    }

    private async Task CallNotifyNewVouchAsync(int recipientId, int voucherId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.NotifyNewVouchAsync(recipientId, voucherId);
    }

    private async Task<int> CallGetUnreadCountAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationReadService svc = scope.ServiceProvider.GetRequiredService<INotificationReadService>();
        return await svc.GetUnreadCountAsync();
    }

    private async Task<NotificationDto[]> CallGetNotificationsAsync(int page, int pageSize)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationReadService svc = scope.ServiceProvider.GetRequiredService<INotificationReadService>();
        return await svc.GetNotificationsAsync(page, pageSize);
    }

    private async Task<NotificationDto[]> CallGetNotificationsAsync(
        int page, int pageSize, NotificationFeedOrder order)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationReadService svc = scope.ServiceProvider.GetRequiredService<INotificationReadService>();
        return await svc.GetNotificationsAsync(page, pageSize, order);
    }

    private async Task<int> CallGetTotalCountAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationReadService svc = scope.ServiceProvider.GetRequiredService<INotificationReadService>();
        return await svc.GetTotalCountAsync();
    }

    /// <summary>
    /// Seeds a raw <c>SiteAnnouncement</c> notification row directly via
    /// <see cref="ApplicationDbContext"/>. Used to test the RelatedEntityKind.None branch
    /// (no semantic write method exists for SiteAnnouncement yet).
    /// <c>RelatedEntityId = 0</c> — valid int, no FK constraint on the polymorphic column.
    /// <c>SourceUserId = null</c> — announcements have no actor.
    /// </summary>
    private async Task SeedAnnouncementNotificationAsync(int recipientId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Notifications.Add(new Notification
        {
            RecipientUserId    = recipientId,
            NotificationTypeId = NotificationTypeEnum.SiteAnnouncement,
            SourceUserId       = null,
            RelatedEntityId    = 0,
            IsRead             = false,
            DateCreated        = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Pins a notification's <c>DateCreated</c> to a fixed instant so ordering tests assert the
    /// sort, not the wall clock. Back-to-back writes can land on the same timestamp (Windows'
    /// ~15 ms clock tick); setting the value explicitly is deterministic and replaces the old
    /// <c>Task.Delay</c>-and-hope hack.
    /// </summary>
    private async Task SetNotificationDateCreatedAsync(
        int recipientId, NotificationTypeEnum type, int sourceUserId, DateTime dateCreated)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Notification row = await db.Notifications.SingleAsync(n =>
            n.RecipientUserId == recipientId &&
            n.NotificationTypeId == type &&
            n.SourceUserId == sourceUserId);
        row.DateCreated = dateCreated;
        await db.SaveChangesAsync();
    }

    private async Task CallMarkAsReadAsync(long notificationId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.MarkAsReadAsync(notificationId);
    }

    private async Task CallMarkAllAsReadAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.MarkAllAsReadAsync();
    }

    private async Task<NotificationSettingDto[]> CallGetSettingsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationReadService svc = scope.ServiceProvider.GetRequiredService<INotificationReadService>();
        return await svc.GetSettingsAsync();
    }

    private async Task CallSetSettingAsync(NotificationTypeEnum type, bool emailEnabled, bool collapsed)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.SetSettingAsync(type, emailEnabled, collapsed);
    }

    private async Task CallFollowAsync(int targetUserId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IFollowingWriteService svc = scope.ServiceProvider.GetRequiredService<IFollowingWriteService>();
        await svc.FollowAsync(targetUserId);
    }

    // ── DB assertion helpers ──────────────────────────────────────────────────────

    private async Task<Notification?> GetNotificationAsync(
        int recipientId, NotificationTypeEnum type, int sourceUserId, int relatedEntityId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.FirstOrDefaultAsync(n =>
            n.RecipientUserId == recipientId &&
            n.NotificationTypeId == type &&
            n.SourceUserId == sourceUserId &&
            n.RelatedEntityId == relatedEntityId);
    }

    private async Task<int> CountNotificationsAsync(
        int recipientId, NotificationTypeEnum type, int sourceUserId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n =>
            n.RecipientUserId == recipientId &&
            n.NotificationTypeId == type &&
            n.SourceUserId == sourceUserId);
    }

    private async Task<bool> IsNotificationUnreadAsync(long notificationId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications
            .Where(n => n.NotificationId == notificationId)
            .Select(n => !n.IsRead)
            .FirstOrDefaultAsync();
    }
}
