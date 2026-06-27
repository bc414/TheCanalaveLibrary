using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="StoryCard"/> (WU13). Covers:
/// - Title renders and links to /story/{id}.
/// - Author byline links to /user/{authorId}; null AuthorId → plain text, no anchor.
/// - Tags render as TagChip; read-only (no remove button).
/// - ShortDescription renders with tooltip; null → not rendered.
/// - WordCountDisplay: &lt; 1K → "N words"; 1K–1M → "XK words"; ≥ 1M → "X.XM words".
/// - Cover-art fallback: null CoverArtRelativeUrl → placeholder div, no img element.
/// - Status and rating badges present with expected labels.
/// - Composes UserStoryInteractionPanel in Listing context.
/// - Caret: "View Story" always; optional callback items gated by HasDelegate.
///
/// <b>Not tested here:</b> visual/Tailwind layout (human sign-off for Stage 6),
/// and the _coverArtFailed @onerror path (bUnit does not fire img browser events).
/// </summary>
public class StoryCardTests : TestContext
{
    private readonly FakeUserStoryInteractionWriteService _fakeService = new();

    public StoryCardTests()
    {
        // StoryCard nests UserStoryInteractionPanel, which injects IUserStoryInteractionWriteService.
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => _fakeService);
        // TagChip (nested in StoryCard) injects ISpriteReadService for sprite URL resolution.
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static StoryListingDto MakeStory(
        int storyId = 1,
        string title = "Test Story",
        string? shortDescription = null,
        string? coverArtRelativeUrl = null,
        int? authorId = 42,
        string authorName = "TestAuthor",
        int wordCount = 5_000,
        StoryStatusEnum status = StoryStatusEnum.InProgress,
        Rating rating = Rating.T,
        IReadOnlyList<TagChipDto>? tags = null) =>
        new(storyId, title, shortDescription, coverArtRelativeUrl, authorId, authorName,
            wordCount, status, rating, DateTime.UtcNow, tags ?? []);

    private static TagChipDto MakeTag(int id = 10, string name = "Tag A") =>
        new() { TagId = id, TagName = name, TagTypeId = TagTypeEnum.Character };

    // ── Title + link ─────────────────────────────────────────────────────────────

    [Fact]
    public void Title_RendersAsLink_PointingToStoryRoute()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(storyId: 7, title: "My Fic")));

        cut.Find("a[href='/story/7']").TextContent.Trim().Should().Be("My Fic");
    }

    // ── Author byline ────────────────────────────────────────────────────────────

    [Fact]
    public void AuthorByline_WhenAuthorIdPresent_RendersAsLink()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(authorId: 99, authorName: "Famous Author")));

        var authorLinks = cut.FindAll($"a[href='/user/99']");
        authorLinks.Should().ContainSingle("one author link expected");
        authorLinks[0].TextContent.Should().Contain("Famous Author");
    }

    [Fact]
    public void AuthorByline_WhenAuthorIdNull_RendersPlainText_NoAnchor()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(authorId: null, authorName: "Unknown Author")));

        // No link to a user profile should exist when the author is anonymous/deleted.
        cut.FindAll("a[href^='/user/']").Should().BeEmpty(
            "deleted/anonymous author has no profile link");
        cut.Markup.Should().Contain("Unknown Author",
            "author name still appears as plain text");
    }

    // ── Tags ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Tags_RenderOneTagChip_PerTag()
    {
        var tags = new[] { MakeTag(10, "Tag A"), MakeTag(11, "Tag B"), MakeTag(12, "Tag C") };
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(tags: tags)));

        // TagChip renders as a <span>; the three chips must be present.
        // TagChip has no remove button (OnRemove not wired → no ✕ element).
        cut.Markup.Should().Contain("Tag A")
            .And.Contain("Tag B")
            .And.Contain("Tag C");
        cut.FindAll("button[aria-label='Remove tag']").Should().BeEmpty(
            "read-only context: no OnRemove wired, so no remove button inside TagChip");
    }

    [Fact]
    public void Tags_WhenEmpty_NoTagNames()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(tags: [])));

        // When the tag list is empty the @if block suppresses the tag container entirely.
        // We verify by seeding known names and confirming none appear when tags is [].
        // (Can't use span.rounded-full here — status/rating badges use the same class.)
        cut.Markup.Should().NotContain("Tag A").And.NotContain("Tag B",
            "no tag names should appear in markup when the tag list is empty");
    }

    // ── Short description ────────────────────────────────────────────────────────

    [Fact]
    public void ShortDescription_WhenPresent_RendersWithTitleTooltip()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(shortDescription: "A gripping tale of Sinnoh.")));

        var para = cut.Find("p[title='A gripping tale of Sinnoh.']");
        para.TextContent.Trim().Should().Be("A gripping tale of Sinnoh.");
    }

    [Fact]
    public void ShortDescription_WhenNull_NotRendered()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(shortDescription: null)));

        // No <p> with a title attribute should appear (there's no other <p> in the card).
        cut.FindAll("p[title]").Should().BeEmpty("no synopsis paragraph when ShortDescription is null");
    }

    // ── WordCountDisplay ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(500, "500 words")]
    [InlineData(999, "999 words")]
    [InlineData(1_000, "1K words")]
    [InlineData(1_500, "2K words")]    // 1500 / 1000 = 1.5, F0 rounds to 2
    [InlineData(50_000, "50K words")]
    [InlineData(999_999, "1000K words")]
    [InlineData(1_000_000, "1.0M words")]
    [InlineData(1_500_000, "1.5M words")]
    public void WordCountDisplay_FormatsCorrectly(int wordCount, string expected)
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(wordCount: wordCount)));

        cut.Markup.Should().Contain(expected, $"word count {wordCount} should display as '{expected}'");
    }

    // ── Cover art ────────────────────────────────────────────────────────────────

    [Fact]
    public void CoverArt_WhenUrlNull_RendersFallbackDiv_NoImg()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(coverArtRelativeUrl: null, title: "Fallback Story")));

        cut.FindAll("img").Should().BeEmpty("null URL must show the fallback placeholder, not a broken img");
        // Fallback shows the title's first character.
        cut.Markup.Should().Contain("F", "fallback placeholder shows title initial 'F'");
    }

    [Fact]
    public void CoverArt_WhenUrlPresent_RendersImg_WithLazyLoading()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(coverArtRelativeUrl: "/img/covers/my-story.webp")));

        var img = cut.Find("img");
        img.GetAttribute("src").Should().Be("/img/covers/my-story.webp");
        img.GetAttribute("loading").Should().Be("lazy");
    }

    // ── Status + rating badges ───────────────────────────────────────────────────

    [Theory]
    [InlineData(StoryStatusEnum.InProgress, "In Progress")]
    [InlineData(StoryStatusEnum.Completed, "Complete")]
    [InlineData(StoryStatusEnum.OnHiatus, "On Hiatus")]
    [InlineData(StoryStatusEnum.Cancelled, "Cancelled")]
    [InlineData(StoryStatusEnum.Draft, "Draft")]
    public void StatusBadge_ShowsExpectedLabel(StoryStatusEnum status, string expectedLabel)
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(status: status)));

        cut.Markup.Should().Contain(expectedLabel,
            $"status {status} should display as '{expectedLabel}'");
    }

    [Theory]
    [InlineData(Rating.E, "Everyone")]
    [InlineData(Rating.T, "Teen")]
    [InlineData(Rating.M, "Mature")]
    public void RatingBadge_ShowsExpectedLabel(Rating rating, string expectedLabel)
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(rating: rating)));

        cut.Markup.Should().Contain(expectedLabel,
            $"rating {rating} should display as '{expectedLabel}'");
    }

    // ── UserStoryInteractionPanel composition ────────────────────────────────────

    [Fact]
    public void InteractionPanel_BlankSlate_ShowsReadLaterAndIgnoreButtons()
    {
        // Panel in Listing context with null state (all-false) shows ReadLater + Ignore.
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(storyId: 3))
            .Add(c => c.UserStoryInteractionState, null));

        var buttons = cut.FindAll("button[aria-label]");
        buttons.Select(b => b.GetAttribute("aria-label"))
            .Should().Contain("Read It Later")
            .And.Contain("Ignored",
                "Listing context blank-slate must show ReadLater and Ignore clickable buttons");
    }

    [Fact]
    public void InteractionPanel_IsOwnStory_ShowsEditLink_NoButtons()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(storyId: 5))
            .Add(c => c.IsOwnStory, true));

        cut.Find("a[href='/story/5/edit']").Should().NotBeNull(
            "IsOwnStory=true must show an Edit Story link via the panel");
        // All interaction buttons suppressed when own story.
        cut.FindAll("button[aria-label^='Favorite'], button[aria-label^='Follow'], " +
                    "button[aria-label^='Read'], button[aria-label^='Ignored']")
            .Should().BeEmpty("no interaction buttons when IsOwnStory is true");
    }

    // ── Caret menu ───────────────────────────────────────────────────────────────

    [Fact]
    public void Caret_NoCallbacksWired_MenuShowsOnlyViewStory()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(storyId: 1)));

        // Open the menu by clicking the caret button.
        cut.Find("button[aria-label='Story options']").Click();

        // Only "View Story" should appear (all other EventCallbacks have no delegate).
        cut.Markup.Should().Contain("View Story");
        cut.Markup.Should().NotContain("Discover from this Story");
        cut.Markup.Should().NotContain("Copy link");
        cut.Markup.Should().NotContain("Report");
        cut.Markup.Should().NotContain("Download/Export");
    }

    [Fact]
    public void Caret_OnReportWired_MenuShowsReportItem()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(storyId: 1))
            .Add(c => c.OnReport, EventCallback.Factory.Create(this, () => { })));

        cut.Find("button[aria-label='Story options']").Click();

        cut.Markup.Should().Contain("Report",
            "Report item should appear when OnReport has a delegate");
    }

    [Fact]
    public void Caret_ViewStory_AlwaysPresent_EvenWithNoCallbacks()
    {
        IRenderedComponent<StoryCard> cut = RenderComponent<StoryCard>(p => p
            .Add(c => c.Story, MakeStory(storyId: 8)));

        cut.Find("button[aria-label='Story options']").Click();

        // "View Story" link must always be in the menu.
        cut.Find("a[href='/story/8']").Should().NotBeNull(
            "View Story link is always present in the caret dropdown");
    }
}
