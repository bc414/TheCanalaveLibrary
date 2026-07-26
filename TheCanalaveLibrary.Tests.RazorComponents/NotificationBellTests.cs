using Bunit;
using FluentAssertions;
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
        this.AddAuthorization().SetAuthorized("SeedReader");

        IRenderedComponent<NotificationBell> cut = Render<NotificationBell>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("4", "the unread badge renders from the read service"));
    }
}
