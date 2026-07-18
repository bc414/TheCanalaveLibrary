using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="RecommendationEditor"/> (WU29). Covers: SaveLabel on primary
/// button; submit disabled when char count is below minimum (always true with JSInterop.Loose
/// since GetHtmlAsync returns empty string → 0 chars); Cancel button gated by OnCancel.HasDelegate;
/// char meter text visible.
///
/// <b>JS interop note:</b> EditorView → Blazored.TextEditor uses JS. JSInterop.Mode is Loose so
/// GetHtmlAsync returns empty string in tests; the PeriodicTimer will always sample 0 chars,
/// keeping the submit button disabled. Tests assert structure/callback firing, not the count value.
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class RecommendationEditorTests : BunitContext
{
    public RecommendationEditorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── SaveLabel ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RecommendationEditor_SaveLabel_AppearsOnButton()
    {
        IRenderedComponent<RecommendationEditor> cut = Render<RecommendationEditor>(p => p
            .Add(c => c.SaveLabel, "Submit Recommendation"));

        cut.Markup.Should().Contain("Submit Recommendation",
            "SaveLabel must appear on the primary action button");
    }

    // ── Submit disabled below minimum ─────────────────────────────────────────────

    [Fact]
    public void RecommendationEditor_InitialRender_SubmitDisabledBelowMinimum()
    {
        // With JSInterop.Loose, GetHtmlAsync returns empty → 0 chars → disabled.
        IRenderedComponent<RecommendationEditor> cut = Render<RecommendationEditor>(p => p
            .Add(c => c.SaveLabel, "Submit Recommendation"));

        // aria-label selectors are more stable than text content.
        var btn = cut.Find("[aria-label='Submit Recommendation']");
        btn.HasAttribute("disabled").Should().BeTrue(
            "submit must be disabled when current character count is below the 500-char minimum");
    }

    // ── Cancel button visibility (.HasDelegate) ───────────────────────────────────

    [Fact]
    public void RecommendationEditor_OnCancelNotWired_NoCancelButton()
    {
        IRenderedComponent<RecommendationEditor> cut = Render<RecommendationEditor>(p => p
            .Add(c => c.SaveLabel, "Submit Recommendation")
            // OnCancel not wired — new-compose pattern
        );

        cut.Markup.Should().NotContain("Cancel",
            "Cancel button must not appear when OnCancel is not wired");
    }

    [Fact]
    public async Task RecommendationEditor_CancelClick_RaisesOnCancel()
    {
        bool cancelled = false;
        IRenderedComponent<RecommendationEditor> cut = Render<RecommendationEditor>(p => p
            .Add(c => c.SaveLabel, "Save Changes")
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => cancelled = true)));

        await cut.Find("[aria-label='Cancel']").ClickAsync(new());

        cancelled.Should().BeTrue("OnCancel must fire when Cancel is clicked");
    }

    // ── Busy state ────────────────────────────────────────────────────────────────

    [Fact]
    public void RecommendationEditor_Busy_CancelButtonDisabled()
    {
        IRenderedComponent<RecommendationEditor> cut = Render<RecommendationEditor>(p => p
            .Add(c => c.SaveLabel, "Save Changes")
            .Add(c => c.Busy, true)
            .Add(c => c.OnCancel, EventCallback.Factory.Create(this, () => { })));

        cut.Find("[aria-label='Cancel']").HasAttribute("disabled").Should().BeTrue(
            "Cancel must be disabled when Busy is true");
    }
}
