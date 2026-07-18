using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="StoryPage"/> (WU25; retargeted from the former StoryDesktop
/// composite 2026-07-18, WU-ResponsiveMerge — the page now owns its markup and loads story +
/// supplementary data itself). Covers the §5.28 layout: title, metadata row (author link vs.
/// plain text, status/rating badges via StoryDisplayFormat, word count, tags), cover art,
/// long description via RichTextView, chapter list via ChapterList, author-only Edit Story
/// link (auth-claim driven), and RecommendationSection composition.
///
/// <b>Not tested here:</b> visual/Tailwind layout (human sign-off); the cover art @onerror path
/// (bUnit does not fire browser img events); the view-ping JS registration (loose JSInterop).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class StoryPageTests : BunitContext
{
    private readonly FakeRecommendationWriteService _fakeRecommendations = new();
    private readonly FakeRelatedStoriesStoryReadService _storyReadService = new();
    private readonly FakeChapterReadService _chapterReadService = new();
    private readonly BunitAuthorizationContext _auth;

    public StoryPageTests()
    {
        // Page injections: story details (IStoryReadService), chapter list + watermark
        // (IChapterReadService), per-viewer USI state (IUserStoryInteractionReadService),
        // series/lineage/arcs supplementary reads, view-ping write.
        Services.AddScoped<IStoryReadService>(_ => _storyReadService);
        Services.AddScoped<IChapterReadService>(_ => _chapterReadService);
        Services.AddScoped<IUserStoryInteractionReadService>(_ => new FakeRelatedStoriesInteractionReadService());
        Services.AddScoped<ISeriesReadService>(_ => new FakeSeriesReadService());
        Services.AddScoped<IStoryLineageReadService>(_ => new FakeStoryLineageReadService());
        Services.AddScoped<IStoryArcReadService>(_ => new FakeStoryArcReadService());
        Services.AddScoped<IViewCountWriteService>(_ => new FakeViewCountWriteService());
        // SocialMetaTags (inside the page) injects IPublicUrlProvider (pure Core class).
        Services.AddScoped<IPublicUrlProvider>(_ => new PublicUrlProvider("https://test.local"));
        // RecommendationSection injects IRecommendationWriteService.
        Services.AddScoped<IRecommendationWriteService>(_ => _fakeRecommendations);
        // UserStoryInteractionPanel (authenticated renders) injects the USI write service.
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => new FakeUserStoryInteractionWriteService());
        // TagChip injects ISpriteReadService.
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        // ChapterList (WU45) injects the manual read-mark write service.
        Services.AddSingleton<IChapterReadMarkWriteService>(new FakeChapterReadMarkWriteService());
        // RelatedStoriesSection (Feature 61) injects these; left at empty defaults so the section
        // renders nothing (BothEmpty) — see RelatedStoriesSectionTests for its own coverage.
        Services.AddScoped<ICoOccurrenceReadService>(_ => new FakeCoOccurrenceReadService());
        Services.AddScoped<IDiscoveryDefaultsReadService>(_ => new FakeDiscoveryDefaultsReadService());
        // RichTextView and the view-ping registration use JS interop.
        JSInterop.Mode = JSRuntimeMode.Loose;
        // Supplies the Task<AuthenticationState> cascade the page awaits (anonymous by default;
        // author tests add a NameIdentifier claim matching the story's AuthorId).
        _auth = this.AddAuthorization();
        // Start with an empty recommendation list unless a test overrides this.
        _fakeRecommendations.SetGetForStoryResult([]);
    }

    // ── Factory helpers ───────────────────────────────────────────────────────────

    private static StoryDetailsDTO MakeStory(
        int storyId = 1,
        string title = "A Great Fanfic",
        int? authorId = 42,
        string? authorName = "Famous Author",
        string? coverArtRelativeUrl = null,
        string? longDescription = null,
        int wordCount = 12_000,
        StoryStatusEnum status = StoryStatusEnum.InProgress,
        Rating rating = Rating.T,
        IReadOnlyList<TagChipDto>? tags = null) =>
        new()
        {
            StoryId = storyId,
            StoryTitle = title,
            AuthorId = authorId,
            AuthorName = authorName,
            CoverArtRelativeUrl = coverArtRelativeUrl,
            LongDescription = longDescription,
            WordCount = wordCount,
            PublishDate = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            LastUpdatedDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = status,
            Rating = rating,
            Tags = tags ?? []
        };

    private static TagChipDto MakeTag(int id = 10, string name = "Adventure", TagTypeEnum type = TagTypeEnum.Genre) =>
        new() { TagId = id, TagName = name, TagTypeId = type };

    private static ChapterListEntryDto MakeChapter(int num = 1, string title = "Chapter One") =>
        new(ChapterId: 100 + num, num, title, 3_000, IsPublished: true, PublishDate: null,
            IsRead: false, ReadProgress: 0f, AlternateVersions: []);

    /// <summary>Renders the page for the given story (the page loads it via the fake reads).</summary>
    private IRenderedComponent<StoryPage> RenderPage(
        StoryDetailsDTO story,
        IReadOnlyList<ChapterListEntryDto>? chapters = null)
    {
        _storyReadService.StoryDetails = story;
        _chapterReadService.ChapterList = chapters ?? [];
        return Render<StoryPage>(p => p.Add(c => c.StoryId, story.StoryId));
    }

    /// <summary>Authenticates the viewer as the given user id (drives _isAuthor on the page).</summary>
    private void AuthenticateAs(int userId) =>
        _auth.SetAuthorized($"user-{userId}")
             .SetClaims(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));

    // ── Title ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryPage_Title_RendersInH1()
    {
        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory(title: "The Dragon's Oath"));

        cut.Find("h1").TextContent.Trim().Should().Be("The Dragon's Oath");
    }

    // ── Author byline ────────────────────────────────────────────────────────────

    [Fact]
    public void StoryPage_AuthorWithId_RendersAuthorLink()
    {
        IRenderedComponent<StoryPage> cut = RenderPage(
            MakeStory(authorId: 99, authorName: "PokéWriter"));

        cut.Find("a[href='/user/99']").TextContent.Should().Contain("PokéWriter");
    }

    [Fact]
    public void StoryPage_AuthorWithNullId_RendersPlainTextNoLink()
    {
        IRenderedComponent<StoryPage> cut = RenderPage(
            MakeStory(authorId: null, authorName: "Anonymous"));

        cut.FindAll("a[href^='/user/']").Should().BeEmpty(
            "deleted/anonymous author has no profile link");
        cut.Markup.Should().Contain("Anonymous",
            "author name still appears as plain text");
    }

    // ── Author-only Edit Story link ───────────────────────────────────────────────

    [Fact]
    public void StoryPage_ViewerIsAuthor_RendersEditStoryLink()
    {
        AuthenticateAs(42); // matches MakeStory's default AuthorId

        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory(storyId: 5, authorId: 42));

        cut.Find("a[href='/story/5/edit']").TextContent.Should().Contain("Edit",
            "author must see the Edit Story link");
    }

    // ── Status + rating badges ────────────────────────────────────────────────────

    [Theory]
    [InlineData(StoryStatusEnum.InProgress, "In Progress")]
    [InlineData(StoryStatusEnum.Completed, "Complete")]
    [InlineData(StoryStatusEnum.OnHiatus, "On Hiatus")]
    [InlineData(StoryStatusEnum.Cancelled, "Cancelled")]
    public void StoryPage_StatusBadge_ShowsExpectedLabel(StoryStatusEnum status, string expected)
    {
        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory(status: status));

        cut.Markup.Should().Contain(expected, $"status {status} should display as '{expected}'");
    }

    [Theory]
    [InlineData(Rating.E, "Everyone")]
    [InlineData(Rating.T, "Teen")]
    [InlineData(Rating.M, "Mature")]
    public void StoryPage_RatingBadge_ShowsExpectedLabel(Rating rating, string expected)
    {
        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory(rating: rating));

        cut.Markup.Should().Contain(expected, $"rating {rating} should display as '{expected}'");
    }

    // ── Word count display ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(750, "750 words")]
    [InlineData(5_000, "5K words")]
    [InlineData(2_000_000, "2.0M words")]
    public void StoryPage_WordCountDisplay_FormatsCorrectly(int wordCount, string expected)
    {
        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory(wordCount: wordCount));

        cut.Markup.Should().Contain(expected);
    }

    // ── Tags ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryPage_WithTags_RendersTagChips()
    {
        IReadOnlyList<TagChipDto> tags =
        [
            MakeTag(1, "Adventure"),
            MakeTag(2, "Mystery"),
        ];

        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory(tags: tags));

        cut.Markup.Should().Contain("Adventure").And.Contain("Mystery");
        cut.FindAll("button[aria-label='Remove tag']").Should().BeEmpty(
            "read-only tag chips have no remove button");
    }

    // ── Cover art ────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryPage_CoverUrlPresent_RendersImg()
    {
        IRenderedComponent<StoryPage> cut = RenderPage(
            MakeStory(coverArtRelativeUrl: "/images/covers/great-fanfic.webp"));

        cut.Find("img[src='/images/covers/great-fanfic.webp']").Should().NotBeNull();
    }

    // ── Long description via RichTextView ────────────────────────────────────────

    [Fact]
    public void StoryPage_LongDescriptionPresent_RendersContent()
    {
        IRenderedComponent<StoryPage> cut = RenderPage(
            MakeStory(longDescription: "<p>A gripping adventure across Sinnoh.</p>"));

        cut.Markup.Should().Contain("A gripping adventure across Sinnoh.",
            "long description HTML must be rendered inside RichTextView");
    }

    // ── Chapter list ──────────────────────────────────────────────────────────────

    [Fact]
    public void StoryPage_WithChapters_RendersChapterListSection()
    {
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(1, "Introduction"),
            MakeChapter(2, "Rising Action"),
        ];

        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory(storyId: 7), chapters);

        // ChapterList renders each chapter as a link.
        cut.Find("a[href='/story/7/1']").TextContent.Should().Contain("Introduction");
        cut.Find("a[href='/story/7/2']").TextContent.Should().Contain("Rising Action");
    }

    [Fact]
    public void StoryPage_NoChaptersAndNotAuthor_ChapterSectionAbsent()
    {
        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory(storyId: 3));

        // The Chapters @if renders only when Chapters.Length > 0 || _isAuthor.
        cut.Markup.Should().NotContain("No chapters yet",
            "the chapter section (and its empty-state message) is absent when there are no chapters and viewer is not the author");
    }

    [Fact]
    public void StoryPage_NoChaptersButIsAuthor_ChapterSectionPresent()
    {
        // The author identity forces the section to render even with 0 published chapters,
        // so the author can see the empty state and know to add chapters.
        AuthenticateAs(42);

        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory(storyId: 3, authorId: 42));

        cut.Markup.Should().Contain("Chapters",
            "the chapter section heading must appear for the author even with no published chapters");
        cut.Markup.Should().Contain("No chapters yet",
            "ChapterList shows its empty-state message when the author's list is empty");
    }

    // ── RecommendationSection composition ────────────────────────────────────────

    [Fact]
    public void StoryPage_RendersRecommendationSection()
    {
        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory(storyId: 10));

        // RecommendationSection calls GetForStoryAsync on render — verify service was called.
        _fakeRecommendations.GetForStoryCalls.Should().ContainSingle()
            .Which.Should().Be(10,
                "RecommendationSection must call GetForStoryAsync with the story's StoryId");
    }

    [Fact]
    public void StoryPage_RecommendationSection_CurrentUserIdPassedThrough()
    {
        // Anonymous (no auth claims) → no "Recommend this story" CTA.
        IRenderedComponent<StoryPage> cut = RenderPage(MakeStory());

        cut.Markup.Should().NotContain("Recommend this story",
            "anonymous user must not see the Recommend CTA");
    }
}
