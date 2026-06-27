using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="StoryMobile"/> (WU25). Same spec §5.28 sections as
/// <see cref="StoryDesktop"/>; differs in layout only (mobile = full-width, tighter padding,
/// cover art appears after title rather than after the metadata row). The layout differences
/// are Tailwind/visual and are not verifiable by bUnit — they are covered by the live-server
/// Stage-6 visual review.
///
/// Tests here mirror <see cref="StoryDesktopTests"/> to confirm that the same data contract and
/// component composition hold on the mobile variant.
///
/// <b>Tier:</b> RazorComponents (bUnit, no host or DB).
/// </summary>
public class StoryMobileTests : TestContext
{
    private readonly FakeRecommendationWriteService _fakeRecommendations = new();

    public StoryMobileTests()
    {
        Services.AddScoped<IRecommendationWriteService>(_ => _fakeRecommendations);
        // TagChip (nested in StoryMobile) injects ISpriteReadService.
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        JSInterop.Mode = JSRuntimeMode.Loose;
        _fakeRecommendations.SetGetForStoryResult([]);
    }

    // ── Factory helpers ───────────────────────────────────────────────────────────

    private static StoryDetailsDTO MakeStory(
        int storyId = 1,
        string title = "Sinnoh Tales",
        int? authorId = 42,
        string? authorName = "Mobile Author",
        string? coverArtRelativeUrl = null,
        string? longDescription = null,
        int wordCount = 8_000,
        StoryStatusEnum status = StoryStatusEnum.InProgress,
        Rating rating = Rating.E,
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
            PublishDate = new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc),
            LastUpdatedDate = new DateTime(2025, 6, 20, 0, 0, 0, DateTimeKind.Utc),
            Status = status,
            Rating = rating,
            Tags = tags ?? []
        };

    private static TagChipDto MakeTag(int id = 1, string name = "Character Tag") =>
        new() { TagId = id, TagName = name, TagTypeId = TagTypeEnum.Character };

    private static ChapterListEntryDto MakeChapter(int num = 1, string title = "Chapter One") =>
        new(num, title, 2_000, IsPublished: true, AlternateVersions: []);

    // ── Title ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryMobile_Title_RendersInH1()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(title: "Pikachu's Journey"))
            .Add(c => c.Chapters, []));

        cut.Find("h1").TextContent.Trim().Should().Be("Pikachu's Journey");
    }

    // ── Author byline ────────────────────────────────────────────────────────────

    [Fact]
    public void StoryMobile_AuthorWithId_RendersAuthorLink()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(authorId: 77, authorName: "PokéFanatic"))
            .Add(c => c.Chapters, []));

        cut.Find("a[href='/user/77']").TextContent.Should().Contain("PokéFanatic");
    }

    [Fact]
    public void StoryMobile_AuthorWithNullId_RendersPlainText_NoLink()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(authorId: null, authorName: "Deleted User"))
            .Add(c => c.Chapters, []));

        cut.FindAll("a[href^='/user/']").Should().BeEmpty("anonymous author has no user link");
        cut.Markup.Should().Contain("Deleted User");
    }

    // ── Author-only Edit link ─────────────────────────────────────────────────────

    [Fact]
    public void StoryMobile_IsAuthorTrue_RendersEditLink()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(storyId: 8))
            .Add(c => c.Chapters, [])
            .Add(c => c.IsAuthor, true));

        cut.Find("a[href='/story/8/edit']").Should().NotBeNull("author must see the edit link");
    }

    [Fact]
    public void StoryMobile_IsAuthorFalse_NoEditLink()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(storyId: 8))
            .Add(c => c.Chapters, [])
            .Add(c => c.IsAuthor, false));

        cut.FindAll("a[href='/story/8/edit']").Should().BeEmpty("non-author must not see the edit link");
    }

    // ── Status + rating badges ────────────────────────────────────────────────────

    [Theory]
    [InlineData(StoryStatusEnum.InProgress, "In Progress")]
    [InlineData(StoryStatusEnum.Completed, "Complete")]
    [InlineData(StoryStatusEnum.OnHiatus, "On Hiatus")]
    public void StoryMobile_StatusBadge_ShowsExpectedLabel(StoryStatusEnum status, string expected)
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(status: status))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().Contain(expected);
    }

    [Theory]
    [InlineData(Rating.E, "Everyone")]
    [InlineData(Rating.T, "Teen")]
    [InlineData(Rating.M, "Mature")]
    public void StoryMobile_RatingBadge_ShowsExpectedLabel(Rating rating, string expected)
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(rating: rating))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().Contain(expected);
    }

    // ── Word count display ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(300, "300 words")]
    [InlineData(10_000, "10K words")]
    [InlineData(1_000_000, "1.0M words")]
    public void StoryMobile_WordCountDisplay_FormatsCorrectly(int wordCount, string expected)
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(wordCount: wordCount))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().Contain(expected);
    }

    // ── Tags ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryMobile_WithTags_RendersTagChips()
    {
        IReadOnlyList<TagChipDto> tags =
        [
            MakeTag(1, "Water Pokémon"),
            MakeTag(2, "Johto"),
        ];

        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(tags: tags))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().Contain("Water Pokémon").And.Contain("Johto");
        cut.FindAll("button[aria-label='Remove tag']").Should().BeEmpty(
            "read-only context: tags must not have a remove button");
    }

    [Fact]
    public void StoryMobile_WithNoTags_NoTagNamesInMarkup()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(tags: []))
            .Add(c => c.Chapters, []));

        cut.FindAll("button[aria-label='Remove tag']").Should().BeEmpty();
    }

    // ── Cover art ────────────────────────────────────────────────────────────────

    [Fact]
    public void StoryMobile_CoverUrlPresent_RendersImg()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(coverArtRelativeUrl: "/images/covers/mobile-cover.webp"))
            .Add(c => c.Chapters, []));

        cut.Find("img[src='/images/covers/mobile-cover.webp']").Should().NotBeNull();
    }

    [Fact]
    public void StoryMobile_CoverUrlNull_NoImgRendered()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(coverArtRelativeUrl: null))
            .Add(c => c.Chapters, []));

        cut.FindAll("img[src]").Should().BeEmpty();
    }

    // ── Long description ─────────────────────────────────────────────────────────

    [Fact]
    public void StoryMobile_LongDescriptionPresent_RendersContent()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(longDescription: "<p>A legendary tale of Team Rocket.</p>"))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().Contain("A legendary tale of Team Rocket.");
    }

    [Fact]
    public void StoryMobile_LongDescriptionNull_NoProse()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(longDescription: null))
            .Add(c => c.Chapters, []));

        cut.Markup.Should().NotContain("Team Rocket");
    }

    // ── Chapter list ──────────────────────────────────────────────────────────────

    [Fact]
    public void StoryMobile_WithChapters_RendersChapterLinks()
    {
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(1, "Prologue"),
            MakeChapter(2, "The Journey Begins"),
        ];

        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(storyId: 15))
            .Add(c => c.Chapters, chapters));

        cut.Find("a[href='/story/15/1']").TextContent.Should().Contain("Prologue");
        cut.Find("a[href='/story/15/2']").TextContent.Should().Contain("The Journey Begins");
    }

    [Fact]
    public void StoryMobile_NoChaptersAndNotAuthor_ChapterSectionAbsent()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(storyId: 4))
            .Add(c => c.Chapters, [])
            .Add(c => c.IsAuthor, false));

        cut.Markup.Should().NotContain("No chapters yet");
    }

    [Fact]
    public void StoryMobile_NoChaptersButIsAuthor_ChapterSectionPresent()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(storyId: 4))
            .Add(c => c.Chapters, [])
            .Add(c => c.IsAuthor, true));

        cut.Markup.Should().Contain("No chapters yet",
            "author sees the empty-state chapter list so they know to add chapters");
    }

    // ── RecommendationSection composition ────────────────────────────────────────

    [Fact]
    public void StoryMobile_RendersRecommendationSection()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory(storyId: 20))
            .Add(c => c.Chapters, []));

        _fakeRecommendations.GetForStoryCalls.Should().ContainSingle()
            .Which.Should().Be(20,
                "RecommendationSection must call GetForStoryAsync with the story's StoryId");
    }

    [Fact]
    public void StoryMobile_Anonymous_NoRecommendCTA()
    {
        IRenderedComponent<StoryMobile> cut = RenderComponent<StoryMobile>(p => p
            .Add(c => c.Story, MakeStory())
            .Add(c => c.Chapters, [])
            .Add(c => c.CurrentUserId, (int?)null));

        cut.Markup.Should().NotContain("Recommend this story",
            "anonymous user must not see the Recommend CTA");
    }
}
