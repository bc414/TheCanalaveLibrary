using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Regression net for the Blazor Server circuit-concurrency bug class (found via browser
/// debugging, 2026-07-01; see <c>layer2-services.md</c> §"Read-context concurrency: factory
/// per method" and <c>testing.md</c> §"What the three tiers structurally can't see").
///
/// <para>In a Blazor Server circuit, all components share ONE DI scope, and sibling components'
/// async initialization interleaves — so two services (or two calls on one service) issue
/// queries concurrently. Before read services created a per-method context from
/// <c>IDbContextFactory&lt;ReadOnlyApplicationDbContext&gt;</c>, that shared scoped context threw
/// <c>InvalidOperationException: A second operation was started on this context instance</c>
/// on every authenticated page load (NotificationBell + MessagesNavLink both render in the
/// layout). These tests simulate the circuit shape the bUnit tier can't (no real services) and
/// the rest of this tier doesn't (one call path per scope): one scope, concurrent read calls.</para>
///
/// <para>Note: many-iteration loops aren't needed — before the fix, a scoped context failed
/// deterministically on the second concurrently-started operation, not probabilistically.</para>
/// </summary>
[Collection("Postgres")]
public class ConcurrentReadAccessTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _userId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await SeedUserAsync("Concurrent");
        SetActiveUser(_userId);
    }

    [Fact]
    public async Task TwoServices_SameScope_ConcurrentReads_DoNotCollide()
    {
        // The layout-chrome shape: NotificationBell (INotificationWriteService) and
        // MessagesNavLink (IMessagingReadService) resolved from the same circuit scope,
        // both querying during component initialization.
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService notifications =
            scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        IMessagingReadService messaging =
            scope.ServiceProvider.GetRequiredService<IMessagingReadService>();

        Task<int> unreadNotifications = notifications.GetUnreadCountAsync();
        Task<int> unreadConversations = messaging.GetUnreadConversationCountAsync();

        // Before the factory fix this threw InvalidOperationException ("A second operation was
        // started on this context instance") from whichever call lost the race.
        await Task.WhenAll(unreadNotifications, unreadConversations);

        (await unreadNotifications).Should().Be(0);
        (await unreadConversations).Should().Be(0);
    }

    [Fact]
    public async Task OneService_ConcurrentReads_DoNotCollide()
    {
        // The NotificationBell RefreshAsync shape: two parallel calls on ONE service instance.
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService notifications =
            scope.ServiceProvider.GetRequiredService<INotificationWriteService>();

        Task<int> countTask = notifications.GetUnreadCountAsync();
        Task<NotificationDto[]> previewTask = notifications.GetNotificationsAsync(1, 8);

        await Task.WhenAll(countTask, previewTask);

        (await countTask).Should().Be(0);
        (await previewTask).Should().BeEmpty();
    }

    [Fact]
    public async Task ChromeServices_ConcurrentWithPageService_DoNotCollide()
    {
        // The full page-load shape: chrome (notifications + messaging) interleaved with a page
        // dispatcher's own parallel loads (GroupPage fires three at once via Task.WhenAll).
        using IServiceScope scope = Factory.Services.CreateScope();
        INotificationWriteService notifications =
            scope.ServiceProvider.GetRequiredService<INotificationWriteService>();
        IMessagingReadService messaging =
            scope.ServiceProvider.GetRequiredService<IMessagingReadService>();
        IStoryReadService stories =
            scope.ServiceProvider.GetRequiredService<IStoryReadService>();

        Task<int> bell = notifications.GetUnreadCountAsync();
        Task<int> envelope = messaging.GetUnreadConversationCountAsync();
        Task<(StoryListingDto[] Items, int TotalCount)> page = stories.GetRecentListingsAsync(1, 10);

        await Task.WhenAll(bell, envelope, page);

        (await page).TotalCount.Should().Be(0); // clean DB — no stories seeded
    }
}
