using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="BookshelvesPage"/> (WU27; retargeted from the former
/// BookshelvesDesktop composite 2026-07-18, WU-ResponsiveMerge — the page now owns its markup
/// and loads listings via <see cref="IStoryReadService"/>). Covers:
/// - Tab bar renders all 11 tab links.
/// - Active tab has aria-current="page"; inactive tabs do not.
/// - StoryDeck is rendered with supplied stories.
/// - ResultsFilterPanel is present in the sidebar.
///
/// Not tested: Tailwind visual layout, dynamic accent colors (human sign-off for Stage 6).
/// </summary>
public class BookshelvesPageTests : BunitContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeUsiFakeService = new();
    private readonly FakeStoryReadService _storyReadService = new();

    public BookshelvesPageTests()
    {
        // Page injections: listings + candidate IDs (IStoryReadService), bookshelf IDs + states
        // (IUserStoryInteractionReadService), recommendation ID feeds (IRecommendationReadService).
        Services.AddScoped<IStoryReadService>(_ => _storyReadService);
        Services.AddScoped<IUserStoryInteractionReadService>(_ => new FakeInteractionReadService());
        Services.AddScoped<IRecommendationReadService>(_ => new FakeRecommendationReadService());
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeUsiFakeService);
        // TagSelector inside ResultsFilterPanel injects ITagReadService.
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadService());
        // TagChip and TagSelector inject ISpriteReadService for sprite URL resolution.
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        // ReportDialog (inside the page) injects IModerationWriteService.
        Services.AddScoped<IModerationWriteService>(_ => new FakeModerationWriteService());
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Supplies the Task<AuthenticationState> cascade the page awaits (anonymous is fine —
        // _currentUserId stays null). TagFilter's WU43 flyouts stay off the DOM the same way.
        this.AddAuthorization();
    }

    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static StoryListingDto MakeStory(int id = 1) =>
        new(id, $"Story {id}", null, null, null, "Author", 5_000,
            StoryStatusEnum.InProgress, Rating.T, DateTime.UtcNow, []);

    private IRenderedComponent<BookshelvesPage> RenderPage(
        BookshelfTab activeTab = BookshelfTab.Favorites,
        StoryListingDto[]? items = null)
    {
        StoryListingDto[] listings = items ?? [MakeStory(1), MakeStory(2)];
        _storyReadService.ListingsResult = (listings, listings.Length);
        return Render<BookshelvesPage>(p => p
            .Add(c => c.Tab, BookshelfTabSlug.For(activeTab)));
    }

    // ── Tab bar ──────────────────────────────────────────────────────────────────

    [Fact]
    public void TabBar_RendersAllElevenTabs()
    {
        IRenderedComponent<BookshelvesPage> cut = RenderPage();

        // Each tab is an <a> inside the nav, plus the My Lists cross-link (WU-CustomLists —
        // custom lists are a separate section, not tabs). LINQ filter, not an attribute-value
        // CSS selector (AngleSharp fragility — see testing.md).
        var navLinks = cut.FindAll("nav[aria-label='Bookshelf tabs'] a");
        navLinks.Count(a => a.GetAttribute("href") != "/my-lists")
            .Should().Be(11, "there are 11 bookshelf tabs");
        navLinks.Count(a => a.GetAttribute("href") == "/my-lists")
            .Should().Be(1, "the My Lists cross-link is present");
    }

    [Fact]
    public void TabBar_ActiveTab_HasAriaCurrent()
    {
        IRenderedComponent<BookshelvesPage> cut = RenderPage(BookshelfTab.Favorites);

        var activeLinks = cut.FindAll("nav a[aria-current='page']");
        activeLinks.Should().HaveCount(1, "exactly one tab is active");

        string href = activeLinks[0].GetAttribute("href") ?? "";
        href.Should().Contain(BookshelfTabSlug.For(BookshelfTab.Favorites));
    }

    [Fact]
    public void TabBar_AllTabLinks_HaveCorrectHref()
    {
        IRenderedComponent<BookshelvesPage> cut = RenderPage();

        foreach (BookshelfTab tab in Enum.GetValues<BookshelfTab>())
        {
            string slug = BookshelfTabSlug.For(tab);
            cut.FindAll($"nav a[href='/bookshelves/{slug}']")
                .Should().HaveCount(1, $"tab {tab} must have a link to /bookshelves/{slug}");
        }
    }

    // ── StoryDeck present ────────────────────────────────────────────────────────

    [Fact]
    public void StoryDeck_IsRendered_WithSuppliedStories()
    {
        IRenderedComponent<BookshelvesPage> cut = RenderPage(
            items: [MakeStory(10), MakeStory(11)]);

        cut.FindComponents<StoryDeck>().Should().HaveCount(1);
        cut.FindComponents<StoryCard>().Should().HaveCount(2, "two stories → two cards");
    }

    // ── Filter sidebar present at every viewport (drawer deleted, WU-ResponsiveMerge) ────────

    [Fact]
    public void FilterSidebar_IsAlwaysRendered()
    {
        IRenderedComponent<BookshelvesPage> cut = RenderPage();

        cut.FindComponents<ResultsFilterPanel>().Should().HaveCount(1,
            "the filter panel is a sidebar in the single responsive tree, not a drawer");
    }

    // Mutation sanity: verify the tab bar renders a different active link when activeTab changes.
    [Fact]
    public void TabBar_DifferentActiveTab_ShowsCorrectAriaCurrent()
    {
        IRenderedComponent<BookshelvesPage> cut = RenderPage(BookshelfTab.Completed);

        string href = cut.Find("nav a[aria-current='page']").GetAttribute("href") ?? "";
        href.Should().Contain(BookshelfTabSlug.For(BookshelfTab.Completed));
    }
}
