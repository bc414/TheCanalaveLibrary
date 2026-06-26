using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="BookshelvesMobile"/> (WU27). Covers:
/// - Tab dropdown renders active tab summary with correct slug in every option link.
/// - Active tab entry in dropdown has aria-current="page".
/// - Filter button is present; overlay not rendered when closed.
/// - Filter overlay renders when open; backdrop and panel are present.
/// - Clicking backdrop (OnClick on the overlay div) calls CloseFilter.
/// - StoryDeck is rendered with supplied stories.
///
/// Not tested: Tailwind visual layout, accent color inline styles (human sign-off for Stage 6).
/// </summary>
public class BookshelvesMobileTests : TestContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeUsiService = new();

    public BookshelvesMobileTests()
    {
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeUsiService);
        // TagSelector inside ResultsFilterPanel injects ITagReadService.
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadService());
        // ReportDialog (inside BookshelvesMobile) injects IModerationWriteService.
        Services.AddScoped<IModerationWriteService>(_ => new FakeModerationWriteService());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static StoryListingDto MakeStory(int id = 1) =>
        new(id, $"Story {id}", null, null, null, "Author", 5_000,
            StoryStatusEnum.InProgress, Rating.T, DateTime.UtcNow, []);

    private IRenderedComponent<BookshelvesMobile> RenderMobile(
        BookshelfTab activeTab = BookshelfTab.Favorites,
        StoryListingDto[]? items = null)
    {
        return RenderComponent<BookshelvesMobile>(p => p
            .Add(c => c.Tab, activeTab)
            .Add(c => c.Items, items ?? [MakeStory(1)])
            .Add(c => c.TotalCount, 1)
            .Add(c => c.Filter, new StoryFilterDto()));
    }

    // ── Tab dropdown ─────────────────────────────────────────────────────────────

    [Fact]
    public void Dropdown_RendersAllElevenTabLinks()
    {
        IRenderedComponent<BookshelvesMobile> cut = RenderMobile();

        int linkCount = cut.FindAll("details a").Count;
        linkCount.Should().Be(11, "there are 11 bookshelf tabs in the dropdown");
    }

    [Fact]
    public void Dropdown_ActiveTab_HasAriaCurrent()
    {
        IRenderedComponent<BookshelvesMobile> cut = RenderMobile(BookshelfTab.Following);

        var activeLinks = cut.FindAll("details a[aria-current='page']");
        activeLinks.Should().HaveCount(1);
        activeLinks[0].GetAttribute("href")
            .Should().Contain(BookshelfTabSlug.For(BookshelfTab.Following));
    }

    [Fact]
    public void Dropdown_AllTabLinks_HaveCorrectHref()
    {
        IRenderedComponent<BookshelvesMobile> cut = RenderMobile();

        foreach (BookshelfTab tab in Enum.GetValues<BookshelfTab>())
        {
            string slug = BookshelfTabSlug.For(tab);
            cut.FindAll($"details a[href='/bookshelves/{slug}']")
                .Should().HaveCount(1, $"tab {tab} must have a link in the dropdown");
        }
    }

    // ── Filter overlay ────────────────────────────────────────────────────────────

    [Fact]
    public void FilterOverlay_InitiallyClosed_NotRendered()
    {
        IRenderedComponent<BookshelvesMobile> cut = RenderMobile();

        cut.FindComponents<ResultsFilterPanel>().Should().BeEmpty(
            "ResultsFilterPanel is inside the overlay and must not be rendered when closed");
    }

    [Fact]
    public void FilterButton_Click_OpensOverlay()
    {
        IRenderedComponent<BookshelvesMobile> cut = RenderMobile();

        cut.Find("button[aria-controls='mobile-filter-overlay']").Click();

        cut.FindComponents<ResultsFilterPanel>().Should().HaveCount(1,
            "ResultsFilterPanel renders inside the overlay when open");
    }

    [Fact]
    public void FilterOverlay_Open_RendersBackdropAndPanel()
    {
        IRenderedComponent<BookshelvesMobile> cut = RenderMobile();
        cut.Find("button[aria-controls='mobile-filter-overlay']").Click();

        cut.Find("#mobile-filter-overlay").Should().NotBeNull("backdrop div is rendered");
        cut.FindComponents<ResultsFilterPanel>().Should().HaveCount(1);
    }

    [Fact]
    public void FilterOverlay_BackdropClick_ClosesOverlay()
    {
        IRenderedComponent<BookshelvesMobile> cut = RenderMobile();
        cut.Find("button[aria-controls='mobile-filter-overlay']").Click();

        cut.Find("#mobile-filter-overlay").Click();

        cut.FindComponents<ResultsFilterPanel>().Should().BeEmpty(
            "clicking the backdrop closes the overlay");
    }

    [Fact]
    public void FilterOverlay_CloseButtonClick_ClosesOverlay()
    {
        IRenderedComponent<BookshelvesMobile> cut = RenderMobile();
        cut.Find("button[aria-controls='mobile-filter-overlay']").Click();

        cut.Find("button[aria-label='Close filters']").Click();

        cut.FindComponents<ResultsFilterPanel>().Should().BeEmpty(
            "clicking the close button closes the overlay");
    }

    // ── StoryDeck present ────────────────────────────────────────────────────────

    [Fact]
    public void StoryDeck_IsRendered_WithSuppliedStories()
    {
        IRenderedComponent<BookshelvesMobile> cut = RenderMobile(
            items: [MakeStory(10), MakeStory(11)]);

        cut.FindComponents<StoryDeck>().Should().HaveCount(1);
        cut.FindComponents<StoryCard>().Should().HaveCount(2);
    }

    // Mutation sanity: verify filter overlay is actually gated (renders nothing when closed).
    [Fact]
    public void FilterPanel_AfterOpenAndClose_IsNotRendered()
    {
        IRenderedComponent<BookshelvesMobile> cut = RenderMobile();
        cut.Find("button[aria-controls='mobile-filter-overlay']").Click(); // open
        cut.Find("button[aria-label='Close filters']").Click();             // close

        cut.FindComponents<ResultsFilterPanel>().Should().BeEmpty(
            "overlay renders nothing when closed — verified by closing after opening");
    }
}
