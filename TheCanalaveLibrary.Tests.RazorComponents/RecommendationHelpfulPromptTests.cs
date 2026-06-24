using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="RecommendationHelpfulPrompt"/> (WU29, Feature 30 mint-surface).
/// Covers: renders when not dismissed; "Yes" fires OnRespond(true) and hides; "No thanks" fires
/// OnRespond(false) and hides; "Dismiss" fires OnDismiss and hides.
/// Pure leaf — no service injection.
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class RecommendationHelpfulPromptTests : TestContext
{
    // ── Renders when not dismissed ────────────────────────────────────────────────

    [Fact]
    public void RecommendationHelpfulPrompt_OnRender_ShowsPromptText()
    {
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderComponent<RecommendationHelpfulPrompt>(p => p
            .Add(c => c.RecommendationId, 1));

        cut.Markup.Should().Contain("helpful", "the prompt must mention the concept");
    }

    [Fact]
    public void RecommendationHelpfulPrompt_OnRender_ShowsYesAndNoButtons()
    {
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderComponent<RecommendationHelpfulPrompt>(p => p
            .Add(c => c.RecommendationId, 1));

        cut.Find("[aria-label*='Yes']").Should().NotBeNull();
        cut.Find("[aria-label*='No']").Should().NotBeNull();
    }

    // ── "Yes" fires OnRespond(true) and hides prompt ──────────────────────────────

    [Fact]
    public async Task RecommendationHelpfulPrompt_YesClick_RaisesOnRespondTrue()
    {
        bool? responded = null;
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderComponent<RecommendationHelpfulPrompt>(p => p
            .Add(c => c.RecommendationId, 5)
            .Add(c => c.OnRespond, EventCallback.Factory.Create<bool>(this, v => responded = v)));

        await cut.Find("[aria-label*='Yes']").ClickAsync(new());

        responded.Should().Be(true, "clicking Yes must raise OnRespond with true");
    }

    [Fact]
    public async Task RecommendationHelpfulPrompt_YesClick_HidesPrompt()
    {
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderComponent<RecommendationHelpfulPrompt>(p => p
            .Add(c => c.RecommendationId, 5));

        await cut.Find("[aria-label*='Yes']").ClickAsync(new());

        cut.Markup.Should().NotContain("helpful", "prompt must disappear after Yes");
    }

    // ── "No thanks" fires OnRespond(false) and hides prompt ──────────────────────

    [Fact]
    public async Task RecommendationHelpfulPrompt_NoClick_RaisesOnRespondFalse()
    {
        bool? responded = null;
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderComponent<RecommendationHelpfulPrompt>(p => p
            .Add(c => c.RecommendationId, 5)
            .Add(c => c.OnRespond, EventCallback.Factory.Create<bool>(this, v => responded = v)));

        await cut.Find("[aria-label*='No']").ClickAsync(new());

        responded.Should().Be(false, "clicking No thanks must raise OnRespond with false");
    }

    [Fact]
    public async Task RecommendationHelpfulPrompt_NoClick_HidesPrompt()
    {
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderComponent<RecommendationHelpfulPrompt>(p => p
            .Add(c => c.RecommendationId, 5));

        await cut.Find("[aria-label*='No']").ClickAsync(new());

        cut.Markup.Should().NotContain("helpful");
    }

    // ── Dismiss ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecommendationHelpfulPrompt_DismissClick_RaisesOnDismissAndHides()
    {
        bool dismissed = false;
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderComponent<RecommendationHelpfulPrompt>(p => p
            .Add(c => c.RecommendationId, 5)
            .Add(c => c.OnDismiss, EventCallback.Factory.Create(this, () => dismissed = true)));

        await cut.Find("[aria-label='Dismiss']").ClickAsync(new());

        dismissed.Should().BeTrue("OnDismiss must fire when the dismiss button is clicked");
        cut.Markup.Should().NotContain("helpful", "prompt must disappear after dismiss");
    }
}
