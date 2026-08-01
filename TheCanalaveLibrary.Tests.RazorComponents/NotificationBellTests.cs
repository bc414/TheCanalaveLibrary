using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Regression tests for <see cref="NotificationBell"/> (H5, closed by WU-TagFanon). The bell is
/// an injection-free AuthorizeView wrapper: an ANONYMOUS render must construct nothing that
/// resolves notification services — that gap is exactly what let an anonymous WASM render call
/// the RequireAuthorization() endpoint and crash the chrome error boundary (fixed 2026-07-13,
/// previously unpinned). Tier: RazorComponents (bUnit).
/// </summary>
public class NotificationBellTests : BunitContext
{
    [Fact]
    public void AnonymousRender_ProducesNothing_AndResolvesNoServices()
    {
        // Deliberately NO INotificationReadService/WriteService registration: if the anonymous
        // path constructed NotificationBellInner, bUnit would throw missing-service.
        this.AddAuthorization(); // defaults to unauthenticated

        IRenderedComponent<NotificationBell> cut = Render<NotificationBell>();

        cut.Markup.Trim().Should().BeEmpty("an anonymous viewer gets no bell and no service resolution");
    }

    [Fact]
    public void AuthorizedRender_ShowsUnreadBadge_FromTheFake()
    {
        FakeNotificationWriteService fake = new() { UnreadCount = 4 };
        Services.AddSingleton<INotificationReadService>(fake);
        Services.AddSingleton<INotificationWriteService>(fake);
        // NotificationBellInner injects PersistentComponentState (manual persistence API — it is
        // MainLayout chrome and must not use [PersistentState]; see PersistentStateTestSupport).
        this.AddPersistentComponentState();
        this.AddAuthorization().SetAuthorized("SeedReader");

        IRenderedComponent<NotificationBell> cut = Render<NotificationBell>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("4", "the unread badge renders from the read service"));
    }

    [Fact]
    public void AuthorizedRender_Navigates_RefreshesUnreadCount()
    {
        // WU-AccountEnforcement: NotificationBellInner's header comment always claimed "refresh
        // on mount / navigation", but nothing ever subscribed to LocationChanged — a notification
        // landing mid-session (e.g. the account-status moderator notifications
        // AccountStatusBanner now surfaces alongside) stayed invisible until the next full page
        // load. This pins the fix: a second in-app navigation must re-query the count.
        FakeNotificationWriteService fake = new() { UnreadCount = 2 };
        Services.AddSingleton<INotificationReadService>(fake);
        Services.AddSingleton<INotificationWriteService>(fake);
        Services.AddLogging();
        this.AddPersistentComponentState();
        this.AddAuthorization().SetAuthorized("SeedReader");

        IRenderedComponent<NotificationBell> cut = Render<NotificationBell>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("2"));

        // A moderator-fired notification lands server-side between the initial mount and the
        // next in-app navigation.
        fake.UnreadCount = 5;
        Services.GetRequiredService<NavigationManager>().NavigateTo("/somewhere-else");

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("5"));
    }
}
