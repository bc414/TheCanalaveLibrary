using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="RecommendationHelpfulPrompt"/> — rebuilt for owner ruling D3
/// (WU-InertFeatures): the prompt shows the recommendation itself as a reminder (read-only card:
/// body and recommender), has exactly two controls — Yes (thumbs up) → <c>OnHelpful</c>, X →
/// <c>OnDismiss</c> — and closes after either. "No thanks" is gone (X is the decline).
/// Pure leaf — no service injection. Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class RecommendationHelpfulPromptTests : BunitContext
{
    private static RecommendationDto Rec() => new(
        RecommendationId:      7,
        StoryId:               10,
        Recommender:           new UserCardDto(42, "Tastemaker", null, null, []),
        BodyHtml:              "<p>You will love the third act.</p>",
        LikeCount:             2,
        IsHiddenGem:           false,
        IsHighlightedByAuthor: false,
        SuccessfulRecCount:    0,
        DatePosted:            new DateTime(2026, 1, 1),
        IsLikedByCurrentUser:  false,
        IsOwnRecommendation:   false);

    private IRenderedComponent<RecommendationHelpfulPrompt> RenderPrompt(
        Action? onHelpful = null, Action? onDismiss = null) =>
        Render<RecommendationHelpfulPrompt>(p => p
            .Add(c => c.Recommendation, Rec())
            .Add(c => c.OnHelpful, EventCallback.Factory.Create(this, () => onHelpful?.Invoke()))
            .Add(c => c.OnDismiss, EventCallback.Factory.Create(this, () => onDismiss?.Invoke())));

    [Fact]
    public void Renders_TheRecommendationAsAReminder()
    {
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderPrompt();

        cut.Markup.Should().Contain("You will love the third act.",
            "the RIL may have been long ago — the widget shows the recommendation itself");
        cut.Markup.Should().Contain("Tastemaker");
    }

    [Fact]
    public void HasExactlyTwoControls_YesAndX_NoNoThanks()
    {
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderPrompt();

        // The reminder card contributes only its recommender UserCard's menu caret — no rec actions.
        int cardButtons = cut.FindComponent<RecommendationCard>().FindAll("button").Count;
        (cut.FindAll("button").Count - cardButtons).Should().Be(2, "D3: Yes and X only");
        cut.FindComponent<RecommendationCard>().FindAll("button[aria-label*='Read It Later'], button[aria-label*='ike']")
            .Should().BeEmpty("the reminder card is read-only");
        cut.Markup.Should().NotContain("No thanks");
        cut.Find("[aria-label*='Yes']");
        cut.Find("[aria-label*='Dismiss']");
    }

    [Fact]
    public async Task Yes_RaisesOnHelpful_AndHides()
    {
        bool helpful = false;
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderPrompt(onHelpful: () => helpful = true);

        await cut.Find("[aria-label*='Yes']").ClickAsync(new());

        helpful.Should().BeTrue();
        cut.FindAll("[role='region']").Should().BeEmpty("the widget closes after an answer");
    }

    [Fact]
    public async Task X_RaisesOnDismiss_AndHides()
    {
        bool dismissed = false;
        IRenderedComponent<RecommendationHelpfulPrompt> cut = RenderPrompt(onDismiss: () => dismissed = true);

        await cut.Find("[aria-label*='Dismiss']").ClickAsync(new());

        dismissed.Should().BeTrue("X is the decline");
        cut.FindAll("[role='region']").Should().BeEmpty();
    }
}
