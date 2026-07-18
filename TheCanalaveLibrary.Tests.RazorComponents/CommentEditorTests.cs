using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="CommentEditor"/> (WU20). CommentEditor is a pure leaf wrapping
/// EditorView plus a Save/Cancel row and an optional spoiler checkbox. Tests cover:
/// primary button label = SaveLabel; Cancel renders only when OnCancel is wired; spoiler checkbox
/// conditional on ShowSpoilerToggle; buttons disabled when Busy; OnSave/OnCancel callbacks raised.
///
/// <b>JS interop note:</b> EditorView wraps Blazored.TextEditor (Quill.js) — JSInterop.Mode is
/// set to Loose so invocations return default values (empty string for GetHtmlAsync). Tests assert
/// callback firing, not the HTML content which is always empty in the test harness.
///
/// <b>Not tested here:</b> Tailwind visual rendering (human sign-off for Stage 6).
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class CommentEditorTests : BunitContext
{
    public CommentEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── SaveLabel ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CommentEditor_SaveLabel_AppearsOnPrimaryButton()
    {
        IRenderedComponent<CommentEditor> cut = Render<CommentEditor>(p => p
            .Add(c => c.SaveLabel, "Post Comment"));

        cut.Markup.Should().Contain("Post Comment",
            "the primary button must display the SaveLabel text");
    }

    // ── Cancel button visibility (.HasDelegate idiom) ─────────────────────────────

    [Fact]
    public void CommentEditor_WhenOnCancelNotWired_NoCancelButton()
    {
        IRenderedComponent<CommentEditor> cut = Render<CommentEditor>(p => p
            .Add(c => c.SaveLabel, "Post Comment")
            // OnCancel deliberately not wired — persistent composer pattern
        );

        cut.Markup.Should().NotContain("Cancel",
            "the Cancel button must not render when OnCancel is not wired");
    }

    [Fact]
    public async Task CommentEditor_ClickCancel_InvokesOnCancel()
    {
        bool cancelFired = false;
        IRenderedComponent<CommentEditor> cut = Render<CommentEditor>(p => p
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => { cancelFired = true; })));

        // Find by aria-label — EditorView embeds BlazoredTextEditor which can render its own
        // buttons; aria-label is the stable, unique selector for CommentEditor's own buttons.
        IElement cancelBtn = cut.Find("button[aria-label='Cancel']");
        await cancelBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        cancelFired.Should().BeTrue("clicking Cancel must invoke OnCancel");
    }

    // ── OnSave callback ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CommentEditor_ClickSave_InvokesOnSave()
    {
        bool saveFired = false;
        IRenderedComponent<CommentEditor> cut = Render<CommentEditor>(p => p
            .Add(c => c.OnSave, EventCallback.Factory.Create<string>(this, _ => { saveFired = true; })));

        // Find by aria-label — the save button carries aria-label="@SaveLabel" so it is uniquely
        // addressable even when BlazoredTextEditor renders its own buttons in the same DOM tree.
        IElement saveBtn = cut.Find("button[aria-label='Save']");
        await saveBtn.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        saveFired.Should().BeTrue("clicking the primary button must invoke OnSave");
    }

    // ── Spoiler toggle visibility ─────────────────────────────────────────────────

    [Fact]
    public void CommentEditor_WhenShowSpoilerToggleTrue_CheckboxPresent()
    {
        IRenderedComponent<CommentEditor> cut = Render<CommentEditor>(p => p
            .Add(c => c.ShowSpoilerToggle, true));

        cut.Markup.Should().Contain("spoilers",
            "the spoiler checkbox label must render when ShowSpoilerToggle is true");
        cut.FindAll("input[type=checkbox]").Should().HaveCount(1);
    }

    // ── Busy state ────────────────────────────────────────────────────────────────

    [Fact]
    public void CommentEditor_WhenBusy_SaveButtonDisabled()
    {
        IRenderedComponent<CommentEditor> cut = Render<CommentEditor>(p => p
            .Add(c => c.Busy, true));

        IElement saveBtn = cut.Find("button[aria-label='Save']");
        saveBtn.HasAttribute("disabled").Should().BeTrue(
            "the primary button must be disabled when Busy is true");
    }

    [Fact]
    public void CommentEditor_WhenBusy_CancelButtonDisabled()
    {
        IRenderedComponent<CommentEditor> cut = Render<CommentEditor>(p => p
            .Add(c => c.Busy, true)
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => { })));

        IElement cancelBtn = cut.Find("button[aria-label='Cancel']");
        cancelBtn.HasAttribute("disabled").Should().BeTrue(
            "the Cancel button must also be disabled when Busy is true");
    }

}
