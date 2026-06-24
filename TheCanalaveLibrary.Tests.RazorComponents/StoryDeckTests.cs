using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="StoryDeck"/> (WU14). Covers:
/// - null Stories → loading text rendered; no StoryCard in the tree.
/// - Empty list → EmptyMessage rendered (default and custom); no StoryCard.
/// - Populated list → N StoryCards present inside the grid container.
/// - CurrentUserId match → that card renders the Edit-Story affordance (IsOwnStory path).
/// - CurrentUserId non-match / null → interaction buttons rendered (not Edit Story).
/// - InteractionStates keyed lookup → the correct state slice forwarded per card.
/// - PaginationControls rendered when TotalPages > 1; absent when single page or PageSize unset.
/// - OnPageChanged callback bubbles when the pager emits a page change.
///
/// <b>Not tested here:</b> Tailwind grid/responsive layout (human sign-off for Stage 6),
/// and the loading-spinner visual (only markup content is verified, not CSS).
/// </summary>
public class StoryDeckTests : TestContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeService = new();

    public StoryDeckTests()
    {
        // StoryDeck nests StoryCard → UserStoryInteractionPanel, which injects the write service.
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeService);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static StoryListingDto MakeStory(
        int storyId = 1,
        string title = "Test Story",
        int? authorId = 42) =>
        new(storyId, title, null, null, authorId, "Author", 5_000,
            StoryStatusEnum.InProgress, Rating.T, DateTime.UtcNow, []);

    private static Dictionary<int, UserStoryInteractionStateDto> MakeStates(
        params UserStoryInteractionStateDto[] states) =>
        states.ToDictionary(s => s.StoryId);

    // ── Loading state ────────────────────────────────────────────────────────────

    [Fact]
    public void NullStories_ShowsLoadingText_AndNoStoryCard()
    {
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, (IReadOnlyList<StoryListingDto>?)null));

        cut.FindAll("[class*='Loading'], span").Select(e => e.TextContent)
            .Should().Contain(t => t.Contains("Loading"), "loading text should appear");
        cut.FindComponents<StoryCard>().Should().BeEmpty("no cards during loading");
    }

    // ── Empty state ──────────────────────────────────────────────────────────────

    [Fact]
    public void EmptyStories_ShowsDefaultEmptyMessage_AndNoStoryCard()
    {
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new List<StoryListingDto>()));

        cut.Markup.Should().Contain("No stories found.", "default empty message expected");
        cut.FindComponents<StoryCard>().Should().BeEmpty();
    }

    [Fact]
    public void EmptyStories_WithCustomEmptyMessage_ShowsCustomMessage()
    {
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new List<StoryListingDto>())
            .Add(c => c.EmptyMessage, "Your bookshelf is empty."));

        cut.Markup.Should().Contain("Your bookshelf is empty.");
        cut.Markup.Should().NotContain("No stories found.");
        cut.FindComponents<StoryCard>().Should().BeEmpty();
    }

    // ── Populated state ──────────────────────────────────────────────────────────

    [Fact]
    public void PopulatedStories_RendersOneStoryCardPerStory()
    {
        var stories = new[]
        {
            MakeStory(storyId: 1, title: "Story A"),
            MakeStory(storyId: 2, title: "Story B"),
            MakeStory(storyId: 3, title: "Story C"),
        };

        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, stories));

        cut.FindComponents<StoryCard>().Should().HaveCount(3);
    }

    [Fact]
    public void PopulatedStories_GridContainerPresent()
    {
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new[] { MakeStory() }));

        // Grid class is on the immediate card wrapper — assert both grid and deck wrapper exist
        cut.Find("[class*='grid']").Should().NotBeNull("grid container must be present");
    }

    // ── IsOwnStory forwarding ────────────────────────────────────────────────────

    [Fact]
    public void CurrentUserId_MatchingAuthorId_RendersEditStoryLink()
    {
        // authorId = 42, CurrentUserId = 42 → IsOwnStory = true → Edit Story link
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new[] { MakeStory(storyId: 1, authorId: 42) })
            .Add(c => c.CurrentUserId, 42));

        cut.Markup.Should().Contain("Edit Story", "own-story path renders Edit Story link");
    }

    [Fact]
    public void CurrentUserId_NotMatchingAuthorId_DoesNotRenderEditStoryLink()
    {
        // authorId = 42, CurrentUserId = 99 → IsOwnStory = false → interaction buttons shown
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new[] { MakeStory(storyId: 1, authorId: 42) })
            .Add(c => c.CurrentUserId, 99));

        cut.Markup.Should().NotContain("Edit Story");
    }

    [Fact]
    public void NullCurrentUserId_DoesNotRenderEditStoryLink()
    {
        // Anonymous viewer → IsOwnStory always false
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new[] { MakeStory(storyId: 1, authorId: 42) })
            .Add(c => c.CurrentUserId, (int?)null));

        cut.Markup.Should().NotContain("Edit Story");
    }

    // ── InteractionStates forwarding ─────────────────────────────────────────────
    //
    // In Listing context, only clickable buttons (ReadLater/Ignore) render as <button> with
    // aria-pressed; the rest render as read-only <span>s when active, absent when inactive.
    // The deck forwards the correct slice when the IsFavorite=true state results in a
    // "Favorite" span (active read-only indicator) that would not appear for an all-false state.

    [Fact]
    public void InteractionStates_IsFavorite_RendersActiveFavoriteSpan()
    {
        // Story 1: IsFavorite=true → active Favorite renders as span[aria-label="Favorite"] (read-only)
        // Story 2: all false → no such span
        var story1 = MakeStory(storyId: 1);
        var story2 = MakeStory(storyId: 2);
        var states = MakeStates(
            new UserStoryInteractionStateDto(1, false, false, IsFavorite: true, false, false, false, false),
            UserStoryInteractionStateDto.AllFalse(2));

        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new[] { story1, story2 })
            .Add(c => c.InteractionStates, (IReadOnlyDictionary<int, UserStoryInteractionStateDto>)states));

        // Active Favorite renders as a read-only <span> in Listing context (not a clickable button)
        cut.FindAll("span[aria-label='Favorite']").Should().NotBeEmpty(
            "IsFavorite=true ⇒ the active read-only Favorite span must appear");
    }

    [Fact]
    public void NullInteractionStates_NoActiveFavoriteSpan()
    {
        // Null state → all-false → Favorite is inactive → leaf guard hides it → no span
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new[] { MakeStory(storyId: 1), MakeStory(storyId: 2) })
            .Add(c => c.InteractionStates, (IReadOnlyDictionary<int, UserStoryInteractionStateDto>?)null));

        cut.FindAll("span[aria-label='Favorite']").Should().BeEmpty(
            "null states ⇒ IsFavorite=false ⇒ inactive read-only Favorite does not render");
    }

    // ── Pagination ───────────────────────────────────────────────────────────────

    [Fact]
    public void PaginationControls_RenderedWhenTotalPagesExceedsOne()
    {
        // 25 items, page size 10 → 3 pages → PaginationControls visible
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new[] { MakeStory() })
            .Add(c => c.CurrentPage, 1)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 25));

        cut.FindComponents<PaginationControls>().Should().ContainSingle();
        // The inner nav/button structure must be visible (PaginationControls renders when TotalPages > 1)
        cut.Markup.Should().Contain("aria-label=\"Previous page\"");
    }

    [Fact]
    public void PaginationControls_HiddenWhenSinglePage()
    {
        // 5 items, page size 10 → 1 page → PaginationControls self-hides (renders nothing)
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new[] { MakeStory() })
            .Add(c => c.CurrentPage, 1)
            .Add(c => c.PageSize, 10)
            .Add(c => c.TotalCount, 5));

        // PaginationControls is still in the tree but renders nothing (@if TotalPages > 1)
        cut.Markup.Should().NotContain("aria-label=\"Previous page\"");
    }

    [Fact]
    public void PaginationControls_HiddenWhenPageSizeIsZero()
    {
        // PageSize default = 0 → TotalPages = 0 → PaginationControls self-hides
        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new[] { MakeStory() }));

        cut.Markup.Should().NotContain("aria-label=\"Previous page\"");
    }

    [Fact]
    public void OnPageChanged_BubblesWhenPagerEmitsPageChange()
    {
        var received = new List<int>();

        IRenderedComponent<StoryDeck> cut = RenderComponent<StoryDeck>(p => p
            .Add(c => c.Stories, new[] { MakeStory() })
            .Add(c => c.CurrentPage, 1)
            .Add(c => c.PageSize, 5)
            .Add(c => c.TotalCount, 30)
            .Add(c => c.OnPageChanged, EventCallback.Factory.Create<int>(this, page => received.Add(page))));

        // Click "Next page" — CurrentPage is 1, so next is 2
        cut.Find("[aria-label='Next page']").Click();

        received.Should().ContainSingle().Which.Should().Be(2);
    }
}
