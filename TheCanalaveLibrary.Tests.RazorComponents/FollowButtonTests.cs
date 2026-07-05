using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="FollowButton"/> (WU21). FollowButton is a self-contained-write
/// composite that injects <see cref="IFollowingWriteService"/>. Tests cover: initial follow/unfollow
/// markup; the bell button appearing only when following; click callbacks invoking the service.
///
/// <b>What is NOT tested here:</b> The actual service persistence (covered by
/// <c>FollowingWriteServiceTests</c> in the Integration tier). Tailwind token rendering remains
/// manual sign-off for Stage 6.
/// </summary>
public class FollowButtonTests : BunitContext
{
    private readonly FakeFollowingWriteService _fakeService = new();

    public FollowButtonTests()
    {
        Services.AddScoped<IFollowingWriteService>(_ => _fakeService);
    }

    // ── initial render ───────────────────────────────────────────────────────────

    [Fact]
    public void FollowButton_WhenNotFollowing_RendersFollowLabel()
    {
        IRenderedComponent<FollowButton> cut = Render<FollowButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, false));

        cut.Find("button").TextContent.Trim().Should().Be("Follow");
    }

    [Fact]
    public void FollowButton_WhenFollowing_RendersFollowingLabel()
    {
        IRenderedComponent<FollowButton> cut = Render<FollowButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.ReceiveAlerts, true));

        cut.FindAll("button").First().TextContent.Trim().Should().Be("Following");
    }

    // ── bell button visibility ────────────────────────────────────────────────────

    [Fact]
    public void FollowButton_WhenNotFollowing_NoBellButton()
    {
        IRenderedComponent<FollowButton> cut = Render<FollowButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, false));

        // Bell button has aria-label "Enable alerts" or "Disable alerts".
        cut.FindAll("button[aria-label]").Should().BeEmpty(
            "the bell button must not appear when the viewer is not following");
    }

    [Fact]
    public void FollowButton_WhenFollowing_ShowsBellButton()
    {
        IRenderedComponent<FollowButton> cut = Render<FollowButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.ReceiveAlerts, true));

        IElement? bell = cut.FindAll("button[aria-label]").FirstOrDefault();
        bell.Should().NotBeNull("the bell button must appear when following");
    }

    [Fact]
    public void FollowButton_WhenFollowingWithAlertsOn_BellHasDisableLabel()
    {
        IRenderedComponent<FollowButton> cut = Render<FollowButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.ReceiveAlerts, true));

        IElement bell = cut.Find("button[aria-label]");
        bell.GetAttribute("aria-label").Should().Be("Disable alerts");
    }

    [Fact]
    public void FollowButton_WhenFollowingWithAlertsOff_BellHasEnableLabel()
    {
        IRenderedComponent<FollowButton> cut = Render<FollowButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.ReceiveAlerts, false));

        IElement bell = cut.Find("button[aria-label]");
        bell.GetAttribute("aria-label").Should().Be("Enable alerts");
    }

    // ── click callbacks ───────────────────────────────────────────────────────────

    [Fact]
    public async Task FollowButton_ClickFollow_CallsFollowAsync()
    {
        IRenderedComponent<FollowButton> cut = Render<FollowButton>(p => p
            .Add(c => c.TargetUserId, 99)
            .Add(c => c.IsFollowing, false));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _fakeService.FollowCalls.Should().Contain(99);
    }

    [Fact]
    public async Task FollowButton_ClickUnfollow_CallsUnfollowAsync()
    {
        IRenderedComponent<FollowButton> cut = Render<FollowButton>(p => p
            .Add(c => c.TargetUserId, 99)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.ReceiveAlerts, true));

        await cut.FindAll("button").First().ClickAsync(
            new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _fakeService.UnfollowCalls.Should().Contain(99);
    }

    [Fact]
    public async Task FollowButton_ClickBell_CallsSetReceiveAlertsAsync()
    {
        IRenderedComponent<FollowButton> cut = Render<FollowButton>(p => p
            .Add(c => c.TargetUserId, 99)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.ReceiveAlerts, true));

        IElement bell = cut.Find("button[aria-label]");
        await bell.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        _fakeService.SetAlertsCalls.Should().Contain((99, false),
            "clicking the bell while alerts are on should toggle them off");
    }
}
