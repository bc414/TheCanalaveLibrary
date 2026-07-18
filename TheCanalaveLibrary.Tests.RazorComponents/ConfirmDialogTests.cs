using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ConfirmDialog"/> — the universal confirm/cancel modal (spec §5.30.9).
/// Pins the interaction contract consumers rely on:
/// <list type="bullet">
///   <item>Backdrop click is a cancel: invokes OnCancel and auto-closes.</item>
///   <item>Confirm button invokes OnConfirm, then auto-closes via IsOpenChanged(false).</item>
///   <item>Cancel button invokes OnCancel, then auto-closes via IsOpenChanged(false).</item>
///   <item>Clicks inside the panel do not close the dialog (stopPropagation on the panel div).</item>
/// </list>
/// </summary>
public class ConfirmDialogTests : BunitContext
{
    [Fact]
    public void WhenClosed_RendersNothing()
    {
        IRenderedComponent<ConfirmDialog> cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsOpen, false)
            .Add(c => c.Message, "Are you sure?"));

        cut.Markup.Trim().Should().BeEmpty("the whole dialog is guarded by @if (IsOpen)");
    }

    [Fact]
    public async Task BackdropClick_InvokesOnCancel_AndAutoCloses()
    {
        bool cancelled = false;
        bool? isOpenChangedValue = null;

        IRenderedComponent<ConfirmDialog> cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Message, "Are you sure?")
            .Add(c => c.OnCancel, () => { cancelled = true; })
            .Add(c => c.IsOpenChanged, (bool open) => { isOpenChangedValue = open; }));

        IElement backdrop = cut.Find("div.fixed");
        await cut.InvokeAsync(() => backdrop.Click());

        cancelled.Should().BeTrue("clicking the backdrop is a cancel");
        isOpenChangedValue.Should().Be(false, "the dialog auto-closes after cancel");
        cut.Markup.Trim().Should().BeEmpty("IsOpen is false after the backdrop cancel");
    }

    [Fact]
    public async Task ConfirmClick_InvokesOnConfirm_AndAutoCloses()
    {
        bool confirmed = false;
        bool cancelled = false;
        bool? isOpenChangedValue = null;

        IRenderedComponent<ConfirmDialog> cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Message, "Are you sure?")
            .Add(c => c.OnConfirm, () => { confirmed = true; })
            .Add(c => c.OnCancel, () => { cancelled = true; })
            .Add(c => c.IsOpenChanged, (bool open) => { isOpenChangedValue = open; }));

        IElement confirmButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Confirm");
        await cut.InvokeAsync(() => confirmButton.Click());

        confirmed.Should().BeTrue("the confirm button must invoke OnConfirm");
        cancelled.Should().BeFalse("confirming must not invoke OnCancel");
        isOpenChangedValue.Should().Be(false, "the dialog auto-closes after confirm");
        cut.Markup.Trim().Should().BeEmpty("IsOpen is false after confirming");
    }

    [Fact]
    public async Task CancelButtonClick_InvokesOnCancel_AndAutoCloses()
    {
        bool confirmed = false;
        bool cancelled = false;
        bool? isOpenChangedValue = null;

        IRenderedComponent<ConfirmDialog> cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Message, "Are you sure?")
            .Add(c => c.OnConfirm, () => { confirmed = true; })
            .Add(c => c.OnCancel, () => { cancelled = true; })
            .Add(c => c.IsOpenChanged, (bool open) => { isOpenChangedValue = open; }));

        IElement cancelButton = cut.FindAll("button").First(b => b.TextContent.Trim() == "Cancel");
        await cut.InvokeAsync(() => cancelButton.Click());

        cancelled.Should().BeTrue("the cancel button must invoke OnCancel");
        confirmed.Should().BeFalse("cancelling must not invoke OnConfirm");
        isOpenChangedValue.Should().Be(false, "the dialog auto-closes after cancel");
        cut.Markup.Trim().Should().BeEmpty("IsOpen is false after cancelling");
    }

    [Fact]
    public void ClickInsidePanel_DoesNotReachBackdropCancel()
    {
        bool cancelled = false;

        IRenderedComponent<ConfirmDialog> cut = Render<ConfirmDialog>(p => p
            .Add(c => c.IsOpen, true)
            .Add(c => c.Message, "Are you sure?")
            .Add(c => c.OnCancel, () => { cancelled = true; }));

        // The panel div carries @onclick:stopPropagation, so a click inside never bubbles to the
        // backdrop's cancel handler. bUnit surfaces that as MissingEventHandlerException (no
        // handler on the panel itself, propagation stopped) — the exception IS the contract pin.
        IElement panel = cut.Find("div.max-w-md");
        Action clickPanel = () => panel.Click();

        clickPanel.Should().Throw<Bunit.MissingEventHandlerException>(
            "the panel stops click propagation, so the click must not reach the backdrop handler");
        cancelled.Should().BeFalse("clicks inside the panel must not cancel");
        cut.Markup.Trim().Should().NotBeEmpty("the dialog stays open");
    }
}
