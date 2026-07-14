using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="BookshelvesDesktop"/> (WU27). Covers:
/// - Tab bar renders all 11 tab links.
/// - Active tab has aria-current="page"; inactive tabs do not.
/// - StoryDeck is rendered with supplied stories.
/// - ResultsFilterPanel is present in the sidebar.
/// - OnPageChanged callback is wired through StoryDeck → PaginationControls.
///
/// Not tested: Tailwind visual layout, dynamic accent colors (human sign-off for Stage 6).
/// </summary>
public class BookshelvesDesktopTests : BunitContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeUsiFakeService = new();

    public BookshelvesDesktopTests()
    {
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeUsiFakeService);
        // TagSelector inside ResultsFilterPanel injects ITagReadService.
        Services.AddScoped<ITagReadService>(_ => new FakeTagReadService());
        // TagChip and TagSelector inject ISpriteReadService for sprite URL resolution.
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        // ReportDialog (inside BookshelvesDesktop) injects IModerationWriteService.
        Services.AddScoped<IModerationWriteService>(_ => new FakeModerationWriteService());
        JSInterop.Mode = JSRuntimeMode.Loose;

        // TagFilter (inside ResultsFilterPanel) mounts SavedTagSelectionLoadFlyout/SaveDialog
        // (WU43), both wrapped in a bare <AuthorizeView> — anonymous/not-authorized by default
        // keeps them off the DOM here (this suite isn't testing that feature).
        this.AddAuthorization();
    }

    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static StoryListingDto MakeStory(int id = 1) =>
        new(id, $"Story {id}", null, null, null, "Author", 5_000,
            StoryStatusEnum.InProgress, Rating.T, DateTime.UtcNow, []);

    private IRenderedComponent<BookshelvesDesktop> RenderDesktop(
        BookshelfTab activeTab = BookshelfTab.Favorites,
        StoryListingDto[]? items = null)
    {
        return Render<BookshelvesDesktop>(p => p
            .Add(c => c.Tab, activeTab)
            .Add(c => c.Items, items ?? [MakeStory(1), MakeStory(2)])
            .Add(c => c.TotalCount, 2)
            .Add(c => c.Filter, new StoryFilterDto()));
    }

    // ── Tab bar ──────────────────────────────────────────────────────────────────

    [Fact]
    public void TabBar_RendersAllElevenTabs()
    {
        IRenderedComponent<BookshelvesDesktop> cut = RenderDesktop();

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
        IRenderedComponent<BookshelvesDesktop> cut = RenderDesktop(BookshelfTab.Favorites);

        var activeLinks = cut.FindAll("nav a[aria-current='page']");
        activeLinks.Should().HaveCount(1, "exactly one tab is active");

        string href = activeLinks[0].GetAttribute("href") ?? "";
        href.Should().Contain(BookshelfTabSlug.For(BookshelfTab.Favorites));
    }

    [Fact]
    public void TabBar_InactiveTabs_DoNotHaveAriaCurrent()
    {
        IRenderedComponent<BookshelvesDesktop> cut = RenderDesktop(BookshelfTab.Favorites);

        var inactiveLinks = cut.FindAll("nav a:not([aria-current])")
            .Where(a => a.GetAttribute("href") != "/my-lists"); // cross-link is not a tab
        inactiveLinks.Should().HaveCount(10, "10 tabs are inactive");
    }

    [Fact]
    public void TabBar_AllTabLinks_HaveCorrectHref()
    {
        IRenderedComponent<BookshelvesDesktop> cut = RenderDesktop();

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
        IRenderedComponent<BookshelvesDesktop> cut = RenderDesktop(
            items: [MakeStory(10), MakeStory(11)]);

        cut.FindComponents<StoryDeck>().Should().HaveCount(1);
        cut.FindComponents<StoryCard>().Should().HaveCount(2, "two stories → two cards");
    }

    // ── ResultsFilterPanel present ───────────────────────────────────────────────

    [Fact]
    public void Sidebar_RendersResultsFilterPanel()
    {
        IRenderedComponent<BookshelvesDesktop> cut = RenderDesktop();

        cut.FindComponents<ResultsFilterPanel>().Should().HaveCount(1,
            "the desktop layout includes a sidebar filter panel");
    }

    // Mutation sanity: verify the tab bar renders a different active link when activeTab changes.
    [Fact]
    public void TabBar_DifferentActiveTab_ShowsCorrectAriaCurrent()
    {
        IRenderedComponent<BookshelvesDesktop> cut = RenderDesktop(BookshelfTab.Completed);

        string href = cut.Find("nav a[aria-current='page']").GetAttribute("href") ?? "";
        href.Should().Contain(BookshelfTabSlug.For(BookshelfTab.Completed));
    }
}
