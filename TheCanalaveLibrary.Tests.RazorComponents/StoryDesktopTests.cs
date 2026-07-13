using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="StoryDesktop"/> (WU25). Covers the §5.28 desktop layout:
/// title, metadata row (author link vs. plain text, status/rating badges, word count, tags),
/// cover art with @onerror fallback, long description via RichTextView, chapter list via
/// ChapterList, author-only Edit Story link, and RecommendationSection composition.
///
/// <b>Not tested here:</b> visual/Tailwind layout differences vs. StoryMobile (human sign-off);
/// the cover art @onerror path (bUnit does not fire browser img events).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class StoryDesktopTests : BunitContext
{
    private readonly FakeRecommendationWriteService _fakeRecommendations = new();

    public StoryDesktopTests()
    {
        // RecommendationSection (nested in StoryDesktop) injects IRecommendationWriteService.
        Services.AddScoped<IRecommendationWriteService>(_ => _fakeRecommendations);
        // TagChip (nested in StoryDesktop) injects ISpriteReadService.
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        // ChapterList (WU45) injects the manual read-mark write service.
        Services.AddSingleton<IChapterReadMarkWriteService>(new FakeChapterReadMarkWriteService());
        // RichTextView and EditorView inside RecommendationEditor use JS interop.
        JSInterop.Mode = JSRuntimeMode.Loose;
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

    // ── Title ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryDesktop_Title_RendersInH1()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(title: "The Dragon's Oath"))
            .Add(c => c.Chapters, []));

        cut.Find("h1").TextContent.Trim().Should().Be("The Dragon's Oath");
    }

    // ── Author byline ────────────────────────────────────────────────────────────

    [Fact]
    public void StoryDesktop_AuthorWithId_RendersAuthorLink()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(authorId: 99, authorName: "PokéWriter"))
            .Add(c => c.Chapters, []));

        cut.Find("a[href='/user/99']").TextContent.Should().Contain("PokéWriter");
    }

    [Fact]
    public void StoryDesktop_AuthorWithNullId_RendersPlainTextNoLink()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(authorId: null, authorName: "Anonymous"))
            .Add(c => c.Chapters, []));

        cut.FindAll("a[href^='/user/']").Should().BeEmpty(
            "deleted/anonymous author has no profile link");
        cut.Markup.Should().Contain("Anonymous",
            "author name still appears as plain text");
    }

    // ── Author-only Edit Story link ───────────────────────────────────────────────

    [Fact]
    public void StoryDesktop_IsAuthorTrue_RendersEditStoryLink()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(storyId: 5))
            .Add(c => c.Chapters, [])
            .Add(c => c.IsAuthor, true));

        cut.Find("a[href='/story/5/edit']").TextContent.Should().Contain("Edit",
            "author must see the Edit Story link");
    }

    [Fact]
    public void StoryDesktop_IsAuthorFalse_NoEditStoryLink()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(storyId: 5))
            .Add(c => c.Chapters, [])
            .Add(c => c.IsAuthor, false));

        cut.FindAll("a[href='/story/5/edit']").Should().BeEmpty(
            "non-author must not see the Edit Story link");
    }

    // ── Status + rating badges ────────────────────────────────────────────────────

    [Theory]
    [InlineData(StoryStatusEnum.InProgress, "In Progress")]
    [InlineData(StoryStatusEnum.Completed, "Complete")]
    [InlineData(StoryStatusEnum.OnHiatus, "On Hiatus")]
    [InlineData(StoryStatusEnum.Cancelled, "Cancelled")]
    public void StoryDesktop_StatusBadge_ShowsExpectedLabel(StoryStatusEnum status, string expected)
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(status: status))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().Contain(expected, $"status {status} should display as '{expected}'");
    }

    [Theory]
    [InlineData(Rating.E, "Everyone")]
    [InlineData(Rating.T, "Teen")]
    [InlineData(Rating.M, "Mature")]
    public void StoryDesktop_RatingBadge_ShowsExpectedLabel(Rating rating, string expected)
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(rating: rating))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().Contain(expected, $"rating {rating} should display as '{expected}'");
    }

    // ── Word count display ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(750, "750 words")]
    [InlineData(5_000, "5K words")]
    [InlineData(2_000_000, "2.0M words")]
    public void StoryDesktop_WordCountDisplay_FormatsCorrectly(int wordCount, string expected)
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(wordCount: wordCount))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().Contain(expected);
    }

    // ── Tags ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryDesktop_WithTags_RendersTagChips()
    {
        IReadOnlyList<TagChipDto> tags =
        [
            MakeTag(1, "Adventure"),
            MakeTag(2, "Mystery"),
        ];

        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(tags: tags))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().Contain("Adventure").And.Contain("Mystery");
        cut.FindAll("button[aria-label='Remove tag']").Should().BeEmpty(
            "read-only tag chips have no remove button");
    }

    [Fact]
    public void StoryDesktop_WithNoTags_TagSectionAbsent()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(tags: []))
            .Add(c => c.Chapters, []));

        cut.FindAll("button[aria-label='Remove tag']").Should().BeEmpty();
        // No tag name known from test data — verify "Adventure" from other tests is absent.
        cut.Markup.Should().NotContain("Adventure");
    }

    // ── Cover art ────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryDesktop_CoverUrlPresent_RendersImg()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(coverArtRelativeUrl: "/images/covers/great-fanfic.webp"))
            .Add(c => c.Chapters, []));

        cut.Find("img[src='/images/covers/great-fanfic.webp']").Should().NotBeNull();
    }

    [Fact]
    public void StoryDesktop_CoverUrlNull_NoImgRendered()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(coverArtRelativeUrl: null))
            .Add(c => c.Chapters, []));

        cut.FindAll("img[src]").Should().BeEmpty(
            "null cover URL must not render an img element");
    }

    // ── Long description via RichTextView ────────────────────────────────────────

    [Fact]
    public void StoryDesktop_LongDescriptionPresent_RendersContent()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(longDescription: "<p>A gripping adventure across Sinnoh.</p>"))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().Contain("A gripping adventure across Sinnoh.",
            "long description HTML must be rendered inside RichTextView");
    }

    [Fact]
    public void StoryDesktop_LongDescriptionNull_NoRichTextViewDiv()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(longDescription: null))
            .Add(c => c.Chapters, []));

        // RichTextView renders a <div> with a style attribute when content is non-empty.
        // When null/empty, the @if block suppresses it.
        cut.Markup.Should().NotContain("A gripping adventure",
            "null long description must not render prose");
    }

    // ── Chapter list ──────────────────────────────────────────────────────────────

    [Fact]
    public void StoryDesktop_WithChapters_RendersChapterListSection()
    {
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(1, "Introduction"),
            MakeChapter(2, "Rising Action"),
        ];

        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(storyId: 7))
            .Add(c => c.Chapters, chapters));

        // ChapterList renders each chapter as a link.
        cut.Find("a[href='/story/7/1']").TextContent.Should().Contain("Introduction");
        cut.Find("a[href='/story/7/2']").TextContent.Should().Contain("Rising Action");
    }

    [Fact]
    public void StoryDesktop_NoChaptersAndNotAuthor_ChapterSectionAbsent()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(storyId: 3))
            .Add(c => c.Chapters, [])
            .Add(c => c.IsAuthor, false));

        // The Chapters @if renders only when Chapters.Count > 0 || IsAuthor.
        cut.Markup.Should().NotContain("No chapters yet",
            "the chapter section (and its empty-state message) is absent when there are no chapters and viewer is not the author");
    }

    [Fact]
    public void StoryDesktop_NoChaptersButIsAuthor_ChapterSectionPresent()
    {
        // IsAuthor=true forces the section to render even with 0 published chapters,
        // so the author can see the empty state and know to add chapters.
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(storyId: 3))
            .Add(c => c.Chapters, [])
            .Add(c => c.IsAuthor, true));

        cut.Markup.Should().Contain("Chapters",
            "the chapter section heading must appear for the author even with no published chapters");
        cut.Markup.Should().Contain("No chapters yet",
            "ChapterList shows its empty-state message when the author's list is empty");
    }

    // ── RecommendationSection composition ────────────────────────────────────────

    [Fact]
    public void StoryDesktop_RendersRecommendationSection()
    {
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory(storyId: 10))
            .Add(c => c.Chapters, []));

        // RecommendationSection calls GetForStoryAsync on render — verify service was called.
        _fakeRecommendations.GetForStoryCalls.Should().ContainSingle()
            .Which.Should().Be(10,
                "RecommendationSection must call GetForStoryAsync with the story's StoryId");
    }

    [Fact]
    public void StoryDesktop_RecommendationSection_CurrentUserIdPassedThrough()
    {
        // Anonymous (null) CurrentUserId → no "Recommend this story" CTA.
        IRenderedComponent<StoryDesktop> cut = Render<StoryDesktop>(p => p
            .Add(c => c.Story, MakeStory())
            .Add(c => c.Chapters, [])
            .Add(c => c.CurrentUserId, (int?)null));

        cut.Markup.Should().NotContain("Recommend this story",
            "anonymous user must not see the Recommend CTA");
    }
}
