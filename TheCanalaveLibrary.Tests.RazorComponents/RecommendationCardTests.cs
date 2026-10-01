using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="RecommendationCard"/> (WU29). Covers: recommender attribution;
/// Author's Pick ribbon (IsHighlightedByAuthor); Hidden Gem badge (IsHiddenGem); like button
/// visibility + callback; gated affordances (edit/delete only for IsOwnRecommendation).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class RecommendationCardTests : BunitContext
{
    // ── Fixtures ──────────────────────────────────────────────────────────────────

    private static RecommendationDto PlainRec(bool isOwn = false, bool isLiked = false)
        => new(
            RecommendationId:       1,
            StoryId:                10,
            Recommender:            new UserCardDto(42, "TestWriter", "tagline", null, []),
            BodyHtml:               "<p>Great story!</p>",
            LikeCount:              3,
            IsHiddenGem:            false,
            IsHighlightedByAuthor:  false,
            SuccessfulRecCount:     0,
            DatePosted:             new DateTime(2026, 1, 1),
            IsLikedByCurrentUser:   isLiked,
            IsOwnRecommendation:    isOwn);

    private static RecommendationDto SpotlightedRec()
        => PlainRec() with { IsHighlightedByAuthor = true };

    private static RecommendationDto HiddenGemRec()
        => PlainRec() with { IsHiddenGem = true };

    // ── Recommender attribution ───────────────────────────────────────────────────

    [Fact]
    public void RecommendationCard_WithRecommender_RendersUsername()
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec()));

        cut.Markup.Should().Contain("TestWriter", "recommender username must appear");
    }

    [Fact]
    public void RecommendationCard_NullRecommender_RendersDeletedUserFallback()
    {
        var rec = PlainRec() with { Recommender = null };

        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, rec));

        cut.Markup.Should().Contain("[deleted user]");
    }

    // ── Author's Pick ribbon ──────────────────────────────────────────────────────

    [Fact]
    public void RecommendationCard_IsHighlightedByAuthor_ShowsRibbon()
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, SpotlightedRec()));

        cut.Markup.Should().Contain("Author's Pick",
            "spotlighted recommendation must display the ribbon");
    }

    // ── Hidden Gem badge ──────────────────────────────────────────────────────────

    [Fact]
    public void RecommendationCard_IsHiddenGem_ShowsBadge()
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, HiddenGemRec()));

        cut.Markup.Should().Contain("Hidden Gem",
            "Hidden Gem badge must appear when IsHiddenGem is true");
    }

    // ── Like button ───────────────────────────────────────────────────────────────

    [Fact]
    public void RecommendationCard_OnLikeWired_ClickRaisesCallbackWithId()
    {
        int? invokedId = null;
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec())
            .Add(c => c.OnLike, EventCallback.Factory.Create<int>(this, id => invokedId = id)));

        cut.Find("[aria-label*='Like']").Click();

        invokedId.Should().Be(1, "OnLike must be raised with the recommendation id");
    }

    [Fact]
    public void RecommendationCard_OnLikeNotWired_NoLikeButton()
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec())
            // OnLike deliberately not wired
        );

        cut.FindAll("[aria-label*='Like']").Count.Should().Be(0, "no like button without OnLike delegate");
    }

    // ── Owner-gated affordances ───────────────────────────────────────────────────

    [Fact]
    public void RecommendationCard_IsOwnRecommendation_ShowsEditAndDelete()
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec(isOwn: true))
            .Add(c => c.OnEdit, EventCallback.Factory.Create<int>(this, _ => { }))
            .Add(c => c.OnDelete, EventCallback.Factory.Create<int>(this, _ => { })));

        cut.Find("[aria-label='Edit recommendation']").Should().NotBeNull();
        cut.Find("[aria-label='Delete recommendation']").Should().NotBeNull();
    }

    // ── Like count + date ─────────────────────────────────────────────────────────

    [Fact]
    public void RecommendationCard_LikeCount_IsRendered()
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec()));

        cut.Markup.Should().Contain("3", "like count must be rendered");
    }

    [Fact]
    public void RecommendationCard_DatePosted_IsRendered()
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec()));

        cut.Markup.Should().Contain("2026", "post date must be rendered");
    }

    // ── Feature 30 reader entry points (owner ruling D3, WU-InertFeatures) ───────

    [Fact]
    public void RecommendationCard_NoReadItLaterDelegate_NoReadItLaterButton()
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec()));

        cut.FindAll("[aria-label*='Read It Later']").Should().BeEmpty(
            "HasDelegate-gated — the read-only reminder in the helpful prompt must not offer it");
        cut.FindAll("a[href*='?rec=']").Should().BeEmpty("Read now is opt-in");
    }

    [Fact]
    public async Task RecommendationCard_ReadItLaterButton_RaisesTheRecommendationId()
    {
        int? raised = null;
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec())
            .Add(c => c.OnReadItLater, EventCallback.Factory.Create<int>(this, id => raised = id)));

        AngleSharp.Dom.IElement button = cut.Find("[aria-label='Save this story to Read It Later']");
        button.GetAttribute("aria-pressed").Should().Be("false");
        await button.ClickAsync(new());

        raised.Should().Be(1);
    }

    [Fact]
    public void RecommendationCard_ReadItLaterSaved_RendersPressedAndInert()
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec())
            .Add(c => c.OnReadItLater, EventCallback.Factory.Create<int>(this, _ => { }))
            .Add(c => c.IsReadItLaterSaved, true));

        AngleSharp.Dom.IElement button = cut.Find("[aria-label='Saved to Read It Later']");
        button.GetAttribute("aria-pressed").Should().Be("true");
        button.HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void RecommendationCard_ShowReadNow_LinksToChapterOneCarryingTheRec()
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec())
            .Add(c => c.ShowReadNow, true));

        cut.Find("a[href='/story/10/1?rec=1']").TextContent.Should().Contain("Read now");
    }

    [Theory]
    [InlineData(RecommendationStatusEnum.NeedsRevision)]
    [InlineData(RecommendationStatusEnum.Rejected)]
    public void RecommendationCard_NotApproved_HidesBothEntryPoints(RecommendationStatusEnum status)
    {
        IRenderedComponent<RecommendationCard> cut = Render<RecommendationCard>(p => p
            .Add(c => c.Rec, PlainRec() with { Status = status })
            .Add(c => c.OnReadItLater, EventCallback.Factory.Create<int>(this, _ => { }))
            .Add(c => c.ShowReadNow, true));

        cut.FindAll("[aria-label*='Read It Later']").Should().BeEmpty("a hidden rec can't be attributed");
        cut.FindAll("a[href*='?rec=']").Should().BeEmpty();
    }
}
