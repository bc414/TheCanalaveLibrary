using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="VouchButton"/> (WU21). Covers: invisible when not following;
/// vouch-button renders when following and not yet vouched; disabled+tooltip when at the vouch
/// limit; "✓ Vouched" state when already vouched; dialog opens on click.
///
/// <b>What is NOT tested here:</b> EditorView interaction inside the dialog (Quill is JS-backed —
/// the rich-text note authoring path is manual/harness-verified per the bUnit JS limitation).
/// Service-persistence is covered by <c>FollowingWriteServiceTests</c> in the Integration tier.
///
/// JSInterop is set to <see cref="JSRuntimeMode.Loose"/> so that Quill-backed EditorView JS calls
/// when the dialog renders do not throw.
/// </summary>
public class VouchButtonTests : TestContext
{
    private readonly FakeFollowingWriteService _fakeService = new();

    public VouchButtonTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<IFollowingWriteService>(_ => _fakeService);
    }

    // ── visibility gate ──────────────────────────────────────────────────────────

    [Fact]
    public void VouchButton_WhenNotFollowing_RendersNothing()
    {
        IRenderedComponent<VouchButton> cut = RenderComponent<VouchButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, false)
            .Add(c => c.IsVouched, false)
            .Add(c => c.AtVouchLimit, false));

        cut.FindAll("button").Should().BeEmpty("VouchButton must render nothing when not following");
    }

    // ── normal vouch state ─────────────────────────────────────────────────────────

    [Fact]
    public void VouchButton_WhenFollowingAndNotVouched_RendersVouchButton()
    {
        IRenderedComponent<VouchButton> cut = RenderComponent<VouchButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.IsVouched, false)
            .Add(c => c.AtVouchLimit, false));

        IElement button = cut.Find("button");
        button.TextContent.Trim().Should().Be("Vouch");
        button.HasAttribute("disabled").Should().BeFalse("the vouch button must be enabled");
    }

    // ── at-limit state ────────────────────────────────────────────────────────────

    [Fact]
    public void VouchButton_WhenAtVouchLimit_RendersDisabledButton()
    {
        IRenderedComponent<VouchButton> cut = RenderComponent<VouchButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.IsVouched, false)
            .Add(c => c.AtVouchLimit, true));

        IElement button = cut.Find("button");
        button.HasAttribute("disabled").Should().BeTrue(
            "VouchButton must be disabled when at the vouch limit");
    }

    [Fact]
    public void VouchButton_WhenAtVouchLimit_HasExplanatoryTooltip()
    {
        IRenderedComponent<VouchButton> cut = RenderComponent<VouchButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.IsVouched, false)
            .Add(c => c.AtVouchLimit, true));

        // The disabled button is wrapped in a <span title="..."> tooltip.
        IElement? tooltipSpan = cut.FindAll("span[title]").FirstOrDefault();
        tooltipSpan.Should().NotBeNull("the disabled button must have an explanatory tooltip span");
        tooltipSpan!.GetAttribute("title").Should().Contain("5 vouches",
            "the tooltip must explain the limit");
    }

    // ── already-vouched state ─────────────────────────────────────────────────────

    [Fact]
    public void VouchButton_WhenAlreadyVouched_RendersVouchedButton()
    {
        IRenderedComponent<VouchButton> cut = RenderComponent<VouchButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.IsVouched, true)
            .Add(c => c.AtVouchLimit, false));

        cut.Find("button").TextContent.Trim().Should().Contain("Vouched",
            "the button must show a 'Vouched' state when IsVouched is true");
    }

    // ── dialog opens ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task VouchButton_ClickVouch_OpensDialog()
    {
        IRenderedComponent<VouchButton> cut = RenderComponent<VouchButton>(p => p
            .Add(c => c.TargetUserId, 42)
            .Add(c => c.IsFollowing, true)
            .Add(c => c.IsVouched, false)
            .Add(c => c.AtVouchLimit, false));

        await cut.Find("button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // ConfirmDialog renders its overlay div when IsOpen = true.
        cut.FindAll("div.fixed").Should().NotBeEmpty("clicking Vouch must open the ConfirmDialog overlay");
    }
}
