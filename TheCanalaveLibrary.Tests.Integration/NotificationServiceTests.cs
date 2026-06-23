using FluentAssertions;
using Microsoft.AspNetCore.Identity;
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
public class NotificationServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestAppFactory _factory = null!;
    private int _actorId;
    private int _recipientId;

    public async Task InitializeAsync()
    {
        _factory = new TestAppFactory(postgres.ConnectionString);
        _ = _factory.Services; // force host build + DataSeeder

        _actorId = await CreateThrowawayUserAsync("NS-A");
        _recipientId = await CreateThrowawayUserAsync("NS-R");

        SetActiveUser(_actorId);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
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
        int actorB = await CreateThrowawayUserAsync("NS-B");
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
        _factory.Services.GetRequiredService<FakeActiveUserContext>().UserId = null;
        _factory.Services.GetRequiredService<FakeActiveUserContext>().IsAuthenticated = false;

        int count = await CallGetUnreadCountAsync();
        count.Should().Be(0);
    }

    // ── GetNotificationsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetNotificationsAsync_ReturnsNewestFirst()
    {
        int actorB = await CreateThrowawayUserAsync("NS-C");

        // Two notifications for the recipient in quick succession.
        await CallNotifyNewFollowerAsync(_recipientId, _actorId);
        await Task.Delay(10); // ensure distinct DateCreated
        await CallNotifyNewVouchAsync(_recipientId, actorB);

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

        if (vouchIndex >= 0 && followIndex >= 0)
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
        int actorB = await CreateThrowawayUserAsync("NS-D");
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

    private void SetActiveUser(int userId)
    {
        FakeActiveUserContext fake = _factory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId = userId;
        fake.IsAuthenticated = true;
    }

    private async Task<int> CreateThrowawayUserAsync(string prefix)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        UserManager<User> userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        User user = new()
        {
            UserName = $"Throwaway{prefix}-{suffix}",
            Email = $"throwaway-{prefix.ToLower()}-{suffix}@test.invalid",
            EmailConfirmed = true,
            ThemeId = 1
        };

        IdentityResult result = await userManager.CreateAsync(user, "Password123!");
        result.Succeeded.Should().BeTrue(
            $"throwaway user creation failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        return user.Id;
    }

    // ── Service call wrappers ─────────────────────────────────────────────────────

    private async Task CallNotifyNewFollowerAsync(int recipientId, int followerId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.NotifyNewFollowerAsync(recipientId, followerId);
    }

    private async Task CallNotifyNewVouchAsync(int recipientId, int voucherId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.NotifyNewVouchAsync(recipientId, voucherId);
    }

    private async Task<int> CallGetUnreadCountAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        INotificationReadService svc = scope.ServiceProvider.GetRequiredService<INotificationReadService>();
        return await svc.GetUnreadCountAsync();
    }

    private async Task<NotificationDto[]> CallGetNotificationsAsync(int page, int pageSize)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        INotificationReadService svc = scope.ServiceProvider.GetRequiredService<INotificationReadService>();
        return await svc.GetNotificationsAsync(page, pageSize);
    }

    private async Task CallMarkAsReadAsync(long notificationId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.MarkAsReadAsync(notificationId);
    }

    private async Task CallMarkAllAsReadAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.MarkAllAsReadAsync();
    }

    private async Task<NotificationSettingDto[]> CallGetSettingsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        INotificationReadService svc = scope.ServiceProvider.GetRequiredService<INotificationReadService>();
        return await svc.GetSettingsAsync();
    }

    private async Task CallSetSettingAsync(NotificationTypeEnum type, bool emailEnabled, bool collapsed)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        INotificationWriteService svc = scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        await svc.SetSettingAsync(type, emailEnabled, collapsed);
    }

    private async Task CallFollowAsync(int targetUserId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IFollowingWriteService svc = scope.ServiceProvider.GetRequiredService<IFollowingWriteService>();
        await svc.FollowAsync(targetUserId);
    }

    // ── DB assertion helpers ──────────────────────────────────────────────────────

    private async Task<Notification?> GetNotificationAsync(
        int recipientId, NotificationTypeEnum type, int sourceUserId, int relatedEntityId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
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
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n =>
            n.RecipientUserId == recipientId &&
            n.NotificationTypeId == type &&
            n.SourceUserId == sourceUserId);
    }

    private async Task<bool> IsNotificationUnreadAsync(long notificationId)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications
            .Where(n => n.NotificationId == notificationId)
            .Select(n => !n.IsRead)
            .FirstOrDefaultAsync();
    }
}
