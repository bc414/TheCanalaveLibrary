using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="MessageComposer"/> (WU35). Covers: SendLabel renders on the
/// primary button; Cancel renders only when OnCancel is wired; Busy disables both buttons;
/// OnSend callback fires when the Send button is clicked.
///
/// <b>JS interop note:</b> MessageComposer wraps EditorView (Blazored.TextEditor / Quill.js) —
/// JSInterop.Mode is set to Loose so invocations return default values (empty string for
/// GetHtmlAsync). Tests assert callback firing, not HTML content.
///
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class MessageComposerTests : TestContext
{
    public MessageComposerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── SendLabel ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MessageComposer_DefaultSendLabel_IsSend()
    {
        IRenderedComponent<MessageComposer> cut = RenderComponent<MessageComposer>();

        cut.Markup.Should().Contain("Send",
            "the primary button must display the default SendLabel");
    }

    [Fact]
    public void MessageComposer_CustomSendLabel_AppearsOnPrimaryButton()
    {
        IRenderedComponent<MessageComposer> cut = RenderComponent<MessageComposer>(p => p
            .Add(c => c.SendLabel, "Send Reply"));

        cut.Markup.Should().Contain("Send Reply",
            "the primary button must display the configured SendLabel");
    }

    // ── Cancel button visibility ──────────────────────────────────────────────────

    [Fact]
    public void MessageComposer_WhenOnCancelNotWired_NoCancelButton()
    {
        IRenderedComponent<MessageComposer> cut = RenderComponent<MessageComposer>();
        // OnCancel not wired — Cancel button must not render.

        cut.Markup.Should().NotContain("Cancel",
            "the Cancel button must not render when OnCancel is not wired");
    }

    [Fact]
    public void MessageComposer_WhenOnCancelWired_CancelButtonPresent()
    {
        IRenderedComponent<MessageComposer> cut = RenderComponent<MessageComposer>(p => p
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => { })));

        cut.Markup.Should().Contain("Cancel",
            "the Cancel button must render when OnCancel is wired");
    }

    // ── OnSend callback ───────────────────────────────────────────────────────────

    [Fact]
    public async Task MessageComposer_ClickSend_InvokesOnSend()
    {
        bool fired = false;
        IRenderedComponent<MessageComposer> cut = RenderComponent<MessageComposer>(p => p
            .Add(c => c.OnSend, EventCallback.Factory.Create<string>(this, _ => { fired = true; }))
            .Add(c => c.SendLabel, "Send"));

        // Select by aria-label — EditorView embeds BlazoredTextEditor with its own toolbar
        // buttons; aria-label is the collision-free stable selector for our Send button.
        IElement sendBtn = cut.Find("button[aria-label='Send']");
        await sendBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        fired.Should().BeTrue("clicking the Send button must invoke OnSend");
    }

    [Fact]
    public async Task MessageComposer_ClickCancel_InvokesOnCancel()
    {
        bool fired = false;
        IRenderedComponent<MessageComposer> cut = RenderComponent<MessageComposer>(p => p
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => { fired = true; })));

        IElement cancelBtn = cut.Find("button[aria-label='Cancel']");
        await cancelBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        fired.Should().BeTrue("clicking Cancel must invoke OnCancel");
    }

    // ── Busy state ────────────────────────────────────────────────────────────────

    [Fact]
    public void MessageComposer_WhenBusy_SendButtonDisabled()
    {
        IRenderedComponent<MessageComposer> cut = RenderComponent<MessageComposer>(p => p
            .Add(c => c.Busy, true)
            .Add(c => c.SendLabel, "Send"));

        IElement sendBtn = cut.Find("button[aria-label='Send']");
        sendBtn.HasAttribute("disabled").Should().BeTrue(
            "the Send button must be disabled when Busy is true");
    }

    [Fact]
    public void MessageComposer_WhenBusy_CancelButtonDisabled()
    {
        IRenderedComponent<MessageComposer> cut = RenderComponent<MessageComposer>(p => p
            .Add(c => c.Busy, true)
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => { })));

        IElement cancelBtn = cut.Find("button[aria-label='Cancel']");
        cancelBtn.HasAttribute("disabled").Should().BeTrue(
            "the Cancel button must also be disabled when Busy is true");
    }

    [Fact]
    public void MessageComposer_WhenNotBusy_SendButtonEnabled()
    {
        IRenderedComponent<MessageComposer> cut = RenderComponent<MessageComposer>(p => p
            .Add(c => c.Busy, false)
            .Add(c => c.SendLabel, "Send"));

        IElement sendBtn = cut.Find("button[aria-label='Send']");
        sendBtn.HasAttribute("disabled").Should().BeFalse(
            "the Send button must be enabled when Busy is false");
    }
}
