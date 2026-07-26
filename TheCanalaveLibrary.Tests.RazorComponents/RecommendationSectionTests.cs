using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="RecommendationSection"/> (WU29). Covers: initial load;
/// empty-state message; cards rendered; authenticated user sees "Recommend" CTA;
/// anonymous user sees no CTA; already-recommended user sees no CTA; optimistic like
/// reconciliation; delete-confirm dialog fires service; Hidden-Gem toggle fires service.
///
/// <b>JS interop note:</b> JSInterop.Mode is Loose — EditorView inside RecommendationEditor
/// uses JS; tests assert service calls, not HTML content.
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class RecommendationSectionTests : BunitContext
{
    private readonly FakeRecommendationWriteService _fakeService = new();

    public RecommendationSectionTests()
    {
        Services.AddScoped<IRecommendationWriteService>(_ => _fakeService);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static RecommendationDto MakeRec(int id, int? storyId = null,
        bool isOwn = false, bool isLiked = false, bool isHighlighted = false,
        RecommendationStatusEnum status = RecommendationStatusEnum.Approved, string? note = null)
        => new(
            RecommendationId:       id,
            StoryId:                storyId ?? 99,
            Recommender:            new UserCardDto(42, "Writer", null, null, []),
            BodyHtml:               $"<p>Body {id}</p>",
            LikeCount:              0,
            IsHiddenGem:            false,
            IsHighlightedByAuthor:  isHighlighted,
            SuccessfulRecCount:     0,
            DatePosted:             DateTime.UtcNow,
            IsLikedByCurrentUser:   isLiked,
            IsOwnRecommendation:    isOwn,
            Status:                 status,
            RevisionRequestNote:    note);

    // ── Initial load ──────────────────────────────────────────────────────────────

    [Fact]
    public void RecommendationSection_OnRender_CallsGetForStoryWithCorrectId()
    {
        _fakeService.SetGetForStoryResult([]);

        Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 7));

        _fakeService.GetForStoryCalls.Should().ContainSingle()
            .Which.Should().Be(7, "GetForStoryAsync must be called with the section's StoryId");
    }

    // ── Three-state display ───────────────────────────────────────────────────────

    [Fact]
    public void RecommendationSection_EmptyResult_ShowsNoRecommendationsMessage()
    {
        _fakeService.SetGetForStoryResult([]);

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1));

        cut.Markup.Should().Contain("No recommendations yet");
    }

    [Fact]
    public void RecommendationSection_WithRecs_RendersCards()
    {
        _fakeService.SetGetForStoryResult([MakeRec(1), MakeRec(2)]);

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1));

        cut.Markup.Should().Contain("Body 1").And.Contain("Body 2");
    }

    // ── "Recommend" CTA gating ────────────────────────────────────────────────────

    [Fact]
    public void RecommendationSection_AuthenticatedNotRecommended_ShowsRecommendCTA()
    {
        _fakeService.SetGetForStoryResult([]);

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.CurrentUserId, 77));

        cut.Markup.Should().Contain("Recommend this story",
            "authenticated users who haven't recommended yet see the CTA");
    }

    [Fact]
    public void RecommendationSection_Anonymous_NoRecommendCTA()
    {
        _fakeService.SetGetForStoryResult([]);

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.CurrentUserId, (int?)null));

        cut.Markup.Should().NotContain("Recommend this story",
            "anonymous users must not see the CTA");
    }

    [Fact]
    public void RecommendationSection_AlreadyRecommended_NoRecommendCTA()
    {
        // The viewer's own recommendation is in the result set.
        _fakeService.SetGetForStoryResult([MakeRec(1, isOwn: true)]);

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.CurrentUserId, 42));

        cut.Markup.Should().NotContain("Recommend this story",
            "users who already have a recommendation must not see the CTA again");
    }

    // ── Optimistic like ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RecommendationSection_LikeClick_CallsToggleLike()
    {
        _fakeService.SetGetForStoryResult([MakeRec(3)]);
        _fakeService.SetLikeResult(new RecommendationLikeResultDto(1, true));

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.CurrentUserId, 77));

        // Click the like button on the card.
        await cut.Find("[aria-label*='Like']").ClickAsync(new());

        _fakeService.ToggleLikeCalls.Should().ContainSingle()
            .Which.Should().Be(3, "ToggleLikeAsync must be called with the recommendation id");
    }

    // ── Delete flow ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecommendationSection_DeleteConfirm_CallsDeleteAsync()
    {
        _fakeService.SetGetForStoryResult([MakeRec(5, isOwn: true)]);

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.CurrentUserId, 42));

        // Click Delete on the card — this opens the ConfirmDialog.
        await cut.Find("[aria-label='Delete recommendation']").ClickAsync(new());
        // Use FindComponent<ConfirmDialog>() to scope the button search to the dialog only —
        // the card ALSO has a "Delete" button, so a flat FindAll would return the card's button
        // first and clicking it would just re-open the dialog rather than confirming. [0]=Cancel, [1]=Delete.
        IRenderedComponent<ConfirmDialog> dialog = cut.FindComponent<ConfirmDialog>();
        await dialog.FindAll("button")[1].ClickAsync(new());

        _fakeService.DeleteCalls.Should().ContainSingle()
            .Which.Should().Be(5, "DeleteAsync must be called with the recommendation id after confirm");
    }

    // ── Hidden-Gem toggle ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RecommendationSection_HiddenGemToggle_CallsSetHiddenGem()
    {
        _fakeService.SetGetForStoryResult([MakeRec(7, isOwn: true)]);

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.CurrentUserId, 42));

        await cut.Find("[aria-label*='Hidden Gem']").ClickAsync(new());

        _fakeService.SetHiddenGemCalls.Should().ContainSingle(
            "SetHiddenGemAsync must be called when the toggle is clicked");
        _fakeService.SetHiddenGemCalls[0].Id.Should().Be(7);
    }

    // ── Author lifecycle actions (WU-RecLifecycle) ────────────────────────────────

    /// <summary>Renders the section as the STORY AUTHOR (user 42) viewing someone else's rec.</summary>
    private IRenderedComponent<RecommendationSection> RenderAsStoryAuthor(RecommendationDto rec)
    {
        _fakeService.SetGetForStoryResult([rec]);
        return Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.StoryAuthorId, 42)
            .Add(c => c.CurrentUserId, 42));
    }

    [Fact]
    public async Task RecommendationSection_RequestRevisionConfirm_CallsServiceWithNote()
    {
        IRenderedComponent<RecommendationSection> cut = RenderAsStoryAuthor(MakeRec(11));

        await cut.Find("[aria-label='Request a revision of this recommendation']").ClickAsync(new());
        // The inline note panel opens (ModSubmissionsPage reject-panel pattern).
        cut.Find("textarea").Input("Please fix the spoiler.");
        await cut.FindAll("button").First(b => b.TextContent.Contains("Send revision request"))
            .ClickAsync(new());

        _fakeService.RequestRevisionCalls.Should().ContainSingle();
        _fakeService.RequestRevisionCalls[0].Id.Should().Be(11);
        _fakeService.RequestRevisionCalls[0].Note.Should().Be("Please fix the spoiler.");
    }

    [Fact]
    public async Task RecommendationSection_RemoveConfirm_CallsRemoveAsync()
    {
        IRenderedComponent<RecommendationSection> cut = RenderAsStoryAuthor(MakeRec(12));

        await cut.Find("[aria-label='Remove this recommendation from your story']").ClickAsync(new());
        // Second ConfirmDialog instance is the Remove dialog ([0] = delete-own). [0]=Cancel, [1]=Remove.
        IRenderedComponent<ConfirmDialog> dialog = cut.FindComponents<ConfirmDialog>()[1];
        await dialog.FindAll("button")[1].ClickAsync(new());

        _fakeService.RemoveCalls.Should().ContainSingle()
            .Which.Should().Be(12, "RemoveAsync must be called after the author confirms");
    }

    [Fact]
    public async Task RecommendationSection_UnblockClick_CallsUnblockAsync()
    {
        IRenderedComponent<RecommendationSection> cut =
            RenderAsStoryAuthor(MakeRec(13, status: RecommendationStatusEnum.Rejected));

        await cut.Find("[aria-label='Unblock this recommendation and restore it']").ClickAsync(new());

        _fakeService.UnblockCalls.Should().ContainSingle()
            .Which.Should().Be(13, "UnblockAsync fires directly — no confirm needed for a reversible restore");
    }

    [Fact]
    public void RecommendationSection_PublicViewer_SeesNoAuthorLifecycleActions()
    {
        _fakeService.SetGetForStoryResult([MakeRec(14)]);

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.StoryAuthorId, 42)
            .Add(c => c.CurrentUserId, 77)); // signed in, but not the story author

        cut.FindAll("[aria-label='Request a revision of this recommendation']").Should().BeEmpty();
        cut.FindAll("[aria-label='Remove this recommendation from your story']").Should().BeEmpty();
    }

    [Fact]
    public void RecommendationSection_StoryAuthor_SeesNoRecommendCTAOnOwnStory()
    {
        _fakeService.SetGetForStoryResult([]);

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.StoryAuthorId, 42)
            .Add(c => c.CurrentUserId, 42));

        cut.Markup.Should().NotContain("Recommend this story",
            "self-recommendation is blocked server-side — don't offer an affordance that can only fail");
    }

    [Fact]
    public void RecommendationSection_RecommenderView_ShowsRevisionNote()
    {
        _fakeService.SetGetForStoryResult([
            MakeRec(15, isOwn: true, status: RecommendationStatusEnum.NeedsRevision, note: "trim the ending detail")]);

        IRenderedComponent<RecommendationSection> cut = Render<RecommendationSection>(p => p
            .Add(c => c.StoryId, 1)
            .Add(c => c.CurrentUserId, 42));

        cut.Markup.Should().Contain("Revision requested",
            "the recommender sees their hidden rec's status");
        cut.Markup.Should().Contain("trim the ending detail",
            "the author's note is displayed inline on the card");
    }
}
