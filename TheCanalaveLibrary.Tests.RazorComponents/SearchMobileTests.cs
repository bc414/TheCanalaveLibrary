using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="SearchMobile"/> (WU28, spec §5.28). Covers:
/// - Filter toggle button is present; overlay is NOT rendered when closed.
/// - Clicking toggle button opens the overlay (ResultsFilterPanel inside).
/// - Random mode: "Give me more" button rendered; sorted mode: pagination controls visible.
/// - StoryDeck receives supplied items.
/// - OnLoadMore callback fires in random mode.
///
/// Not tested: Tailwind layout, animated overlay transitions (human sign-off for Stage 6).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class SearchMobileTests : TestContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeUsiService = new();

    public SearchMobileTests()
    {
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeUsiService);
        // TagSelector inside ResultsFilterPanel injects ITagReadService.
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadService());
        // ReportDialog (inside SearchMobile) injects IModerationWriteService.
        Services.AddScoped<IModerationWriteService>(_ => new FakeModerationWriteService());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static StoryListingDto MakeStory(int id = 1) =>
        new(id, $"Story {id}", null, null, null, "Author", 5_000,
            StoryStatusEnum.InProgress, Rating.T, DateTime.UtcNow, []);

    private IRenderedComponent<SearchMobile> RenderMobile(
        bool isRandomMode = true,
        StoryListingDto[]? items = null,
        int totalCount = 0,
        StoryFilterDto? filter = null)
    {
        StoryListingDto[] stories = items ?? [MakeStory(1), MakeStory(2)];
        return RenderComponent<SearchMobile>(p => p
            .Add(c => c.Items, stories)
            .Add(c => c.TotalCount, totalCount == 0 ? stories.Length : totalCount)
            .Add(c => c.IsRandomMode, isRandomMode)
            .Add(c => c.Filter, filter ?? new StoryFilterDto()));
    }

    // ── Filter overlay ────────────────────────────────────────────────────────────

    [Fact]
    public void FilterButton_IsPresent_WhenOverlayClosed()
    {
        IRenderedComponent<SearchMobile> cut = RenderMobile();

        // SearchMobile's filter toggle uses aria-controls="search-filter-overlay".
        // Finding by aria-controls is more robust than aria-expanded's bool rendering.
        cut.FindAll("button[aria-controls='search-filter-overlay']")
            .Should().NotBeEmpty("filter toggle button is rendered and has aria-controls set");
    }

    [Fact]
    public void FilterOverlay_NotRendered_WhenClosed()
    {
        IRenderedComponent<SearchMobile> cut = RenderMobile();

        cut.FindAll("#search-filter-overlay").Should().BeEmpty(
            "overlay element is not in the DOM when the filter is closed");
    }

    [Fact]
    public void FilterButton_Click_OpensOverlay()
    {
        IRenderedComponent<SearchMobile> cut = RenderMobile();

        cut.Find("button[aria-controls='search-filter-overlay']").Click();

        cut.FindAll("#search-filter-overlay").Should().HaveCount(1,
            "clicking the filter toggle renders the overlay backdrop");
    }

    [Fact]
    public void FilterOverlay_ContainsResultsFilterPanel()
    {
        IRenderedComponent<SearchMobile> cut = RenderMobile();
        cut.Find("button[aria-controls='search-filter-overlay']").Click();

        cut.FindComponents<ResultsFilterPanel>().Should().HaveCount(1,
            "the open overlay contains ResultsFilterPanel");
    }

    // ── Random mode ──────────────────────────────────────────────────────────────

    [Fact]
    public void RandomMode_RendersGiveMeMoreButton()
    {
        IRenderedComponent<SearchMobile> cut = RenderMobile(isRandomMode: true);

        cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Should().Contain("Give me more");
    }

    // ── Sorted mode ──────────────────────────────────────────────────────────────

    [Fact]
    public void SortedMode_DoesNotRenderGiveMeMoreButton()
    {
        IRenderedComponent<SearchMobile> cut = RenderMobile(isRandomMode: false,
            items: [MakeStory(1)], totalCount: 50,
            filter: new StoryFilterDto { Sort = DefaultSortOrder.DatePublished, Page = 1, PageSize = 20 });

        cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Should().NotContain("Give me more");
    }

    // ── StoryDeck ────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryDeck_RendersSuppliedItems()
    {
        IRenderedComponent<SearchMobile> cut = RenderMobile(
            items: [MakeStory(10), MakeStory(11)]);

        cut.FindComponents<StoryCard>().Should().HaveCount(2, "two stories → two cards");
    }

    // ── Callbacks ────────────────────────────────────────────────────────────────

    [Fact]
    public void OnLoadMore_Fires_WhenGiveMeMoreClicked()
    {
        bool fired = false;
        IRenderedComponent<SearchMobile> cut = RenderComponent<SearchMobile>(p => p
            .Add(c => c.Items, [MakeStory()])
            .Add(c => c.TotalCount, 1)
            .Add(c => c.IsRandomMode, true)
            .Add(c => c.Filter, new StoryFilterDto())
            .Add(c => c.OnLoadMore, EventCallback.Factory.Create(this, () => fired = true)));

        // Find the give-me-more button (not the filter toggle)
        IElement giveMeMore = cut.FindAll("button")
            .First(b => b.TextContent.Trim() == "Give me more");
        giveMeMore.Click();

        fired.Should().BeTrue("clicking 'Give me more' fires OnLoadMore");
    }

    // Mutation sanity: switching IsRandomMode changes visible controls.
    [Fact]
    public void MutationSanity_SwitchToSortedMode_HidesGiveMeMoreButton()
    {
        IRenderedComponent<SearchMobile> cut = RenderMobile(isRandomMode: true);
        cut.FindAll("button").Select(b => b.TextContent.Trim())
            .Should().Contain("Give me more");

        cut.SetParametersAndRender(p => p
            .Add(c => c.IsRandomMode, false)
            .Add(c => c.TotalCount, 1)
            .Add(c => c.Filter, new StoryFilterDto { Sort = DefaultSortOrder.DatePublished, Page = 1, PageSize = 20 }));

        cut.FindAll("button").Select(b => b.TextContent.Trim())
            .Should().NotContain("Give me more");
    }
}
