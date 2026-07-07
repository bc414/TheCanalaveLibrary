using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Tests for the transient toast channel (<see cref="ToastService"/> + <see cref="ToastHost"/>,
/// cross-cutting.md §"Error Handling Strategy" — feedback channels): a Show renders a toast in
/// the aria-live region, manual dismiss removes it, and the per-toast duration auto-dismisses.
/// </summary>
public class ToastHostTests : BunitContext
{
    private readonly ToastService _service = new();

    public ToastHostTests()
    {
        Services.AddSingleton<IToastService>(_service);
    }

    [Fact]
    public void EmptyHost_RendersLiveRegionWithNoToasts()
    {
        IRenderedComponent<ToastHost> cut = Render<ToastHost>();

        cut.Find("[aria-live='polite']").ChildElementCount.Should().Be(0);
    }

    [Fact]
    public void Show_RendersToastText_WithStatusRole()
    {
        IRenderedComponent<ToastHost> cut = Render<ToastHost>();

        _service.Show("Draft restored.", ToastLevel.Success, TimeSpan.FromMinutes(5));

        cut.WaitForAssertion(() => cut.Find("[role='status']").TextContent.Should().Contain("Draft restored."));
    }

    [Fact]
    public void DismissButton_RemovesTheToast()
    {
        IRenderedComponent<ToastHost> cut = Render<ToastHost>();
        _service.Show("Something transient.", duration: TimeSpan.FromMinutes(5));
        cut.WaitForAssertion(() => cut.FindAll("[role='status']").Should().HaveCount(1));

        cut.Find("button[aria-label='Dismiss']").Click();

        cut.FindAll("[role='status']").Should().BeEmpty();
    }

    [Fact]
    public void Duration_AutoDismissesTheToast()
    {
        IRenderedComponent<ToastHost> cut = Render<ToastHost>();

        _service.Show("Blink and you miss it.", duration: TimeSpan.FromMilliseconds(50));

        cut.WaitForAssertion(() => cut.FindAll("[role='status']").Should().HaveCount(1));
        cut.WaitForAssertion(() => cut.FindAll("[role='status']").Should().BeEmpty(),
            timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ShowWithNoSubscriber_IsDroppedSilently()
    {
        // Static-SSR pages have no rendered host — Show must be a no-op, not a crash.
        var lonely = new ToastService();
        Action act = () => lonely.Show("nobody listening");
        act.Should().NotThrow();
    }
}
