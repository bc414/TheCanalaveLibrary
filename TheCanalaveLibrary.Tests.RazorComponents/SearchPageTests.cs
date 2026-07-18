using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="SearchPage"/> (WU28, spec §5.28; retargeted from the former
/// SearchDesktop composite 2026-07-18, WU-ResponsiveMerge — the page now owns its markup and
/// loads batches/listings via <see cref="IStoryReadService"/>). Covers:
/// - Random mode (the page default): "Give me more" button is rendered; pagination self-hides.
/// - Sorted mode (via the filter panel's OnSearch): pagination controls are shown.
/// - StoryDeck receives supplied items.
/// - "Give me more" appends another random batch (accumulation path).
///
/// Not tested: Tailwind layout, live §8.7 defaults pre-applied (host/DB; Integration tier),
/// L4 visual sign-off (human, Stage 6).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class SearchPageTests : BunitContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeUsiService = new();
    private readonly FakeStoryReadService _storyReadService = new();

    public SearchPageTests()
    {
        // Page injections: random batches + sorted listings (IStoryReadService), per-viewer
        // states (IUserStoryInteractionReadService), §8.7 defaults (IDiscoveryDefaultsReadService),
        // tag chip resolution (ITagReadService).
        Services.AddScoped<IStoryReadService>(_ => _storyReadService);
        Services.AddScoped<IUserStoryInteractionReadService>(_ => new FakeInteractionReadService());
        Services.AddScoped<IDiscoveryDefaultsReadService>(_ => new FakeDiscoveryDefaultsReadService());
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeUsiService);
        // TagSelector inside ResultsFilterPanel injects ITagReadService.
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadService());
        // TagChip and TagSelector inject ISpriteReadService for sprite URL resolution.
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        // ReportDialog (inside the page) injects IModerationWriteService.
        Services.AddScoped<IModerationWriteService>(_ => new FakeModerationWriteService());
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Supplies the Task<AuthenticationState> cascade the page awaits (anonymous is fine —
        // discovery is public). TagFilter's WU43 flyouts stay off the DOM the same way.
        this.AddAuthorization();
    }

    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static StoryListingDto MakeStory(int id = 1) =>
        new(id, $"Story {id}", null, null, null, "Author", 5_000,
            StoryStatusEnum.InProgress, Rating.T, DateTime.UtcNow, []);

    private IRenderedComponent<SearchPage> RenderPage(StoryListingDto[]? items = null)
    {
        // The page boots in random mode and preloads a batch (spec §5.28: never blank).
        _storyReadService.RandomBatch = items ?? [MakeStory(1), MakeStory(2)];
        return Render<SearchPage>();
    }

    // ── Random mode (page default) ───────────────────────────────────────────────

    [Fact]
    public void RandomMode_RendersGiveMeMoreButton()
    {
        IRenderedComponent<SearchPage> cut = RenderPage();

        cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Should().Contain("Give me more", "random mode exposes the give-me-more control");
    }

    [Fact]
    public void RandomMode_PaginationControlsSelfHide()
    {
        // In random mode TotalCount = Items.Length and PageSize = Items.Length, so TotalPages=1.
        // PaginationControls renders nothing when TotalPages == 1.
        IRenderedComponent<SearchPage> cut = RenderPage(
            items: [MakeStory(1), MakeStory(2)]);

        cut.FindComponents<PaginationControls>().Should().HaveCount(1,
            "PaginationControls component is present (but empty) in random mode");
        // PaginationControls uses an @if (TotalPages > 1) guard that hides the Prev/Next buttons.
        cut.FindAll("button[aria-label='Previous page']").Should().BeEmpty(
            "PaginationControls renders nothing when TotalPages == 1; Prev button is absent");
    }

    // ── Sorted mode ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SortedMode_RendersPaginationControls_WhenMultiplePages()
    {
        IRenderedComponent<SearchPage> cut = RenderPage(items: [MakeStory(1)]);
        _storyReadService.ListingsResult = ([MakeStory(1)], 50);

        // Switch to sorted mode the way the user does — the filter panel's Apply (OnSearch).
        IRenderedComponent<ResultsFilterPanel> panel = cut.FindComponent<ResultsFilterPanel>();
        await cut.InvokeAsync(() => panel.Instance.OnSearch.InvokeAsync(
            new StoryFilterDto { Sort = DefaultSortOrder.DatePublished, Page = 1, PageSize = 20 }));

        cut.FindComponents<PaginationControls>().Should().HaveCount(1);
        // With 50 total and 20 per page, TotalPages = 3 → the Previous/Next buttons render.
        // PaginationControls does not use a <nav> element; check for its "Previous page" button.
        cut.FindAll("button[aria-label='Previous page']").Should().HaveCount(1,
            "PaginationControls renders a Prev button when there are multiple pages in sorted mode");
    }

    // ── StoryDeck ────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryDeck_RendersSuppliedItems()
    {
        IRenderedComponent<SearchPage> cut = RenderPage(
            items: [MakeStory(10), MakeStory(11)]);

        cut.FindComponents<StoryDeck>().Should().HaveCount(1);
        cut.FindComponents<StoryCard>().Should().HaveCount(2, "two stories → two cards");
    }

    // ── Give me more (accumulation path) ─────────────────────────────────────────

    [Fact]
    public void GiveMeMore_Click_AppendsAnotherBatch()
    {
        IRenderedComponent<SearchPage> cut = RenderPage(items: [MakeStory(1)]);
        cut.FindComponents<StoryCard>().Should().HaveCount(1, "initial batch has one story");

        _storyReadService.RandomBatch = [MakeStory(2)];

        // Find "Give me more" by text content, not position.
        cut.FindAll("button")
            .First(b => b.TextContent.Trim() == "Give me more")
            .Click();

        cut.FindComponents<StoryCard>().Should().HaveCount(2,
            "clicking 'Give me more' appends the next batch to the accumulated deck");
    }
}
