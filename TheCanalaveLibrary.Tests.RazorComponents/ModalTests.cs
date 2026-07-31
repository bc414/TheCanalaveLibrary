using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="Modal"/> — the shared overlay primitive (extracted WU-A11y,
/// 2026-07-31). Pins the two contracts this WU cares about:
/// <list type="bullet">
///   <item>Naming: a rendered modal always carries an ARIA <c>role="dialog"</c> and an
///     <c>aria-label</c> equal to <c>Title</c>.</item>
///   <item>The deferral, made executable: <c>aria-modal</c> is absent. WU-A11y-Keyboard adds the
///     focus trap and only then does <c>aria-modal="true"</c> become an honest claim — see
///     layer3.5-structure.md "Container Composite".</item>
/// </list>
/// Also pins the shell mechanics every migrated consumer (ConfirmDialog, ReportDialog, etc.)
/// depends on: backdrop click closes, panel click does not, Footer renders when supplied.
/// </summary>
public class ModalTests : BunitContext
{
    private static RenderFragment SimpleContent => builder =>
    {
        builder.OpenElement(0, "p");
        builder.AddContent(1, "Body content");
        builder.CloseElement();
    };

    [Fact]
    public void WhenClosed_RendersNothing()
    {
        IRenderedComponent<Modal> cut = Render<Modal>(p => p
            .Add(c => c.IsOpen, false)
            .Add(c => c.Title, "Example")
            .Add(c => c.ChildContent, SimpleContent));

        cut.Markup.Trim().Should().BeEmpty("the whole shell is guarded by @if (IsOpen)");
    }

    [Fact]
    public void WhenOpen_CarriesDialogRoleAndAccessibleName()
    {
        IRenderedComponent<Modal> cut = Render<Modal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Title, "Report Content")
            .Add(c => c.ChildContent, SimpleContent));

        IElement panel = cut.Find("[role='dialog']");
        panel.GetAttribute("aria-label").Should().Be("Report Content",
            "the accessible name must equal Title — aria-label is prerender-stable under InteractiveAuto");
    }

    [Fact]
    public void WhenOpen_DoesNotCarryAriaModal()
    {
        IRenderedComponent<Modal> cut = Render<Modal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Title, "Example")
            .Add(c => c.ChildContent, SimpleContent));

        // The deferral made executable: no focus trap exists yet (WU-A11y-Keyboard's scope), so
        // asserting aria-modal="true" here would tell assistive tech the background is inert when
        // it isn't — worse than omitting it. This test fails the day someone adds it prematurely.
        cut.Find("[role='dialog']").HasAttribute("aria-modal").Should().BeFalse(
            "aria-modal is deferred to WU-A11y-Keyboard's focus trap, not claimed ahead of it");
    }

    [Fact]
    public void ShowTitleHeadingFalse_SuppressesVisibleHeading_ButKeepsAccessibleName()
    {
        IRenderedComponent<Modal> cut = Render<Modal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Title, "Confirm")
            .Add(c => c.ShowTitleHeading, false)
            .Add(c => c.ChildContent, SimpleContent));

        cut.FindAll("h2").Should().BeEmpty("ShowTitleHeading=false suppresses the visible <h2>");
        cut.Find("[role='dialog']").GetAttribute("aria-label").Should().Be("Confirm",
            "the accessible name is unaffected by whether a visible heading renders");
    }

    [Fact]
    public async Task BackdropClick_InvokesOnCloseAndIsOpenChanged()
    {
        bool closed = false;
        bool? isOpenChangedValue = null;

        IRenderedComponent<Modal> cut = Render<Modal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Title, "Example")
            .Add(c => c.ChildContent, SimpleContent)
            .Add(c => c.OnClose, () => { closed = true; })
            .Add(c => c.IsOpenChanged, (bool open) => { isOpenChangedValue = open; }));

        IElement backdrop = cut.Find("div.fixed");
        await cut.InvokeAsync(() => backdrop.Click());

        isOpenChangedValue.Should().Be(false, "backdrop click closes the modal");
        closed.Should().BeTrue("OnClose fires on backdrop-dismiss");
        cut.Markup.Trim().Should().BeEmpty("IsOpen is false after the backdrop click");
    }

    [Fact]
    public void ClickInsidePanel_DoesNotReachBackdropClose()
    {
        bool? isOpenChangedValue = null;

        IRenderedComponent<Modal> cut = Render<Modal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Title, "Example")
            .Add(c => c.ChildContent, SimpleContent)
            .Add(c => c.IsOpenChanged, (bool open) => { isOpenChangedValue = open; }));

        // The panel carries @onclick:stopPropagation with no handler of its own — bUnit surfaces
        // that as MissingEventHandlerException, which IS the contract pin (mirrors ConfirmDialogTests).
        IElement panel = cut.Find("[role='dialog']");
        Action clickPanel = () => panel.Click();

        clickPanel.Should().Throw<Bunit.MissingEventHandlerException>(
            "the panel stops click propagation, so the click must not reach the backdrop handler");
        isOpenChangedValue.Should().BeNull("clicks inside the panel must not close the modal");
        cut.Markup.Trim().Should().NotBeEmpty("the modal stays open");
    }

    [Fact]
    public void Footer_RendersWhenSupplied_AbsentWhenNot()
    {
        RenderFragment footer = builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddContent(1, "Close");
            builder.CloseElement();
        };

        IRenderedComponent<Modal> withFooter = Render<Modal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Title, "Example")
            .Add(c => c.ChildContent, SimpleContent)
            .Add(c => c.Footer, footer));

        withFooter.FindAll("button").Should().ContainSingle(b => b.TextContent.Trim() == "Close");

        IRenderedComponent<Modal> withoutFooter = Render<Modal>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Title, "Example")
            .Add(c => c.ChildContent, SimpleContent));

        withoutFooter.FindAll("button").Should().BeEmpty("no Footer parameter means no action row");
    }
}
