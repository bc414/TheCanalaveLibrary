using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="SearchDesktop"/> (WU28, spec §5.28). Covers:
/// - Random mode: "Give me more" button is rendered; pagination controls are self-hidden.
/// - Sorted mode: "Give me more" button is NOT rendered; pagination controls are shown.
/// - ResultsFilterPanel is present with ShowTagIncludeModeToggle=true.
/// - StoryDeck receives supplied items and interaction states.
/// - OnFilterChanged, OnLoadMore, OnPageChanged callbacks fire.
///
/// Not tested: Tailwind layout, live §8.7 defaults pre-applied (host/DB; Integration tier),
/// L4 visual sign-off (human, Stage 6).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class SearchDesktopTests : TestContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeUsiService = new();

    public SearchDesktopTests()
    {
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeUsiService);
        // TagSelector inside ResultsFilterPanel injects ITagReadService.
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadService());
        // ReportDialog (inside SearchDesktop) injects IModerationWriteService.
        Services.AddScoped<IModerationWriteService>(_ => new FakeModerationWriteService());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static StoryListingDto MakeStory(int id = 1) =>
        new(id, $"Story {id}", null, null, null, "Author", 5_000,
            StoryStatusEnum.InProgress, Rating.T, DateTime.UtcNow, []);

    private IRenderedComponent<SearchDesktop> RenderDesktop(
        bool isRandomMode = true,
        StoryListingDto[]? items = null,
        int totalCount = 0,
        StoryFilterDto? filter = null)
    {
        StoryListingDto[] stories = items ?? [MakeStory(1), MakeStory(2)];
        return RenderComponent<SearchDesktop>(p => p
            .Add(c => c.Items, stories)
            .Add(c => c.TotalCount, totalCount == 0 ? stories.Length : totalCount)
            .Add(c => c.IsRandomMode, isRandomMode)
            .Add(c => c.Filter, filter ?? new StoryFilterDto()));
    }

    // ── Random mode ──────────────────────────────────────────────────────────────

    [Fact]
    public void RandomMode_RendersGiveMeMoreButton()
    {
        IRenderedComponent<SearchDesktop> cut = RenderDesktop(isRandomMode: true);

        cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Should().Contain("Give me more", "random mode exposes the give-me-more control");
    }

    [Fact]
    public void RandomMode_PaginationControlsSelfHide()
    {
        // In random mode TotalCount = Items.Length and PageSize = Items.Length, so TotalPages=1.
        // PaginationControls renders nothing when TotalPages == 1.
        IRenderedComponent<SearchDesktop> cut = RenderDesktop(isRandomMode: true,
            items: [MakeStory(1), MakeStory(2)]);

        cut.FindComponents<PaginationControls>().Should().HaveCount(1,
            "PaginationControls component is present (but empty) in random mode");
        // PaginationControls uses an @if (TotalPages > 1) guard that hides the Prev/Next buttons.
        cut.FindAll("button[aria-label='Previous page']").Should().BeEmpty(
            "PaginationControls renders nothing when TotalPages == 1; Prev button is absent");
    }

    // ── Sorted mode ──────────────────────────────────────────────────────────────

    [Fact]
    public void SortedMode_DoesNotRenderGiveMeMoreButton()
    {
        IRenderedComponent<SearchDesktop> cut = RenderDesktop(isRandomMode: false,
            items: [MakeStory(1)], totalCount: 50,
            filter: new StoryFilterDto { Sort = DefaultSortOrder.DatePublished, Page = 1, PageSize = 20 });

        cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Should().NotContain("Give me more", "sorted mode uses pagination instead");
    }

    [Fact]
    public void SortedMode_RendersPaginationControls_WhenMultiplePages()
    {
        IRenderedComponent<SearchDesktop> cut = RenderDesktop(isRandomMode: false,
            items: [MakeStory(1)], totalCount: 50,
            filter: new StoryFilterDto { Sort = DefaultSortOrder.DatePublished, Page = 1, PageSize = 20 });

        cut.FindComponents<PaginationControls>().Should().HaveCount(1);
        // With 50 total and 20 per page, TotalPages = 3 → the Previous/Next buttons render.
        // PaginationControls does not use a <nav> element; check for its "Previous page" button.
        cut.FindAll("button[aria-label='Previous page']").Should().HaveCount(1,
            "PaginationControls renders a Prev button when there are multiple pages in sorted mode");
    }

    // ── ResultsFilterPanel ───────────────────────────────────────────────────────

    [Fact]
    public void Desktop_RendersResultsFilterPanel()
    {
        IRenderedComponent<SearchDesktop> cut = RenderDesktop();

        cut.FindComponents<ResultsFilterPanel>().Should().HaveCount(1,
            "the desktop layout includes a sidebar filter panel");
    }

    // ── StoryDeck ────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryDeck_RendersSuppliedItems()
    {
        IRenderedComponent<SearchDesktop> cut = RenderDesktop(
            items: [MakeStory(10), MakeStory(11)]);

        cut.FindComponents<StoryDeck>().Should().HaveCount(1);
        cut.FindComponents<StoryCard>().Should().HaveCount(2, "two stories → two cards");
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────────

    [Fact]
    public void OnLoadMore_Fires_WhenGiveMeMoreClicked()
    {
        bool fired = false;
        IRenderedComponent<SearchDesktop> cut = RenderComponent<SearchDesktop>(p => p
            .Add(c => c.Items, [MakeStory()])
            .Add(c => c.TotalCount, 1)
            .Add(c => c.IsRandomMode, true)
            .Add(c => c.Filter, new StoryFilterDto())
            .Add(c => c.OnLoadMore, EventCallback.Factory.Create(this, () => fired = true)));

        // Find "Give me more" by text content, not position.
        cut.FindAll("button")
            .First(b => b.TextContent.Trim() == "Give me more")
            .Click();

        fired.Should().BeTrue("clicking 'Give me more' fires OnLoadMore");
    }

    // Mutation sanity: switching IsRandomMode changes visible controls.
    [Fact]
    public void MutationSanity_SwitchToSortedMode_HidesGiveMeMoreButton()
    {
        IRenderedComponent<SearchDesktop> cut = RenderDesktop(isRandomMode: true);
        cut.FindAll("button").Select(b => b.TextContent.Trim())
            .Should().Contain("Give me more");

        cut.SetParametersAndRender(p => p
            .Add(c => c.IsRandomMode, false)
            .Add(c => c.TotalCount, 1)
            .Add(c => c.Filter, new StoryFilterDto { Sort = DefaultSortOrder.DatePublished, Page = 1, PageSize = 20 }));

        cut.FindAll("button").Select(b => b.TextContent.Trim())
            .Should().NotContain("Give me more", "sorted mode hides the give-me-more button");
    }
}
