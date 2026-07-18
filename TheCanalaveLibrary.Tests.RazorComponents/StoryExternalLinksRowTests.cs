using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for the "Also posted on" links row (Feature 53 reframe, WU38d): links render
/// with the author-verified checkmark ONLY on verified rows, the row is absent with no links,
/// and — the settled placement — on the story page it sits after the chapter section and before
/// recommendations. Tier: RazorComponents (bUnit).
/// </summary>
public class StoryExternalLinksRowTests : BunitContext
{
    private readonly FakeRelatedStoriesStoryReadService _storyReadService = new();
    private readonly FakeChapterReadService _chapterReadService = new();

    public StoryExternalLinksRowTests()
    {
        // ChapterList (WU45) injects the manual read-mark write service.
        Services.AddSingleton<IChapterReadMarkWriteService>(new FakeChapterReadMarkWriteService());
        // The placement test renders StoryPage (the former StoryDesktop composite was folded
        // into it 2026-07-18, WU-ResponsiveMerge) — same fake surface as StoryPageTests.
        Services.AddScoped<IStoryReadService>(_ => _storyReadService);
        Services.AddScoped<IChapterReadService>(_ => _chapterReadService);
        Services.AddScoped<ISeriesReadService>(_ => new FakeSeriesReadService());
        Services.AddScoped<IStoryLineageReadService>(_ => new FakeStoryLineageReadService());
        Services.AddScoped<IStoryArcReadService>(_ => new FakeStoryArcReadService());
        Services.AddScoped<IViewCountWriteService>(_ => new FakeViewCountWriteService());
        Services.AddScoped<IPublicUrlProvider>(_ => new PublicUrlProvider("https://test.local"));
        Services.AddScoped<IRecommendationWriteService>(_ => new FakeRecommendationWriteService());
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        // RelatedStoriesSection (Feature 61, nested in the page) injects these; left at
        // their empty defaults so the section renders nothing (BothEmpty).
        Services.AddScoped<ICoOccurrenceReadService>(_ => new FakeCoOccurrenceReadService());
        Services.AddScoped<IUserStoryInteractionReadService>(_ => new FakeRelatedStoriesInteractionReadService());
        Services.AddScoped<IDiscoveryDefaultsReadService>(_ => new FakeDiscoveryDefaultsReadService());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static StoryExternalLinkDto Ao3(bool verified = false) =>
        new("Archive of Our Own", "https://archiveofourown.org/works/123", verified);

    // ── Leaf behavior ────────────────────────────────────────────────────────────

    [Fact]
    public void RendersNothing_WhenNoLinks()
    {
        IRenderedComponent<StoryExternalLinksRow> cut = Render<StoryExternalLinksRow>(p => p
            .Add(c => c.Links, (IReadOnlyList<StoryExternalLinkDto>)[]));

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void RendersLinks_WithCheckmarkOnlyWhenVerified()
    {
        IRenderedComponent<StoryExternalLinksRow> cut = Render<StoryExternalLinksRow>(p => p
            .Add(c => c.Links, (IReadOnlyList<StoryExternalLinkDto>)
            [
                Ao3(verified: true),
                new StoryExternalLinkDto("FanFiction.Net", "https://www.fanfiction.net/s/456", false)
            ]));

        cut.Markup.Should().Contain("Also posted on:");
        var anchors = cut.FindAll("a");
        anchors.Should().HaveCount(2);

        // Verified link carries the checkmark + tooltip; the unverified one carries nothing —
        // that visible absence is the community's anti-theft signal.
        anchors[0].QuerySelector("span[title='Author verified']").Should().NotBeNull();
        anchors[1].QuerySelector("span[title='Author verified']").Should().BeNull();
        anchors[0].GetAttribute("rel").Should().Contain("nofollow");
    }

    // ── Settled placement on the story page ──────────────────────────────────────

    [Fact]
    public void OnStoryPage_RowSitsAfterChaptersAndBeforeRecommendations()
    {
        _storyReadService.StoryDetails = new StoryDetailsDTO
        {
            StoryId = 5,
            StoryTitle = "Placed Story",
            AuthorId = 1,
            AuthorName = "A",
            WordCount = 100,
            PublishDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            Status = StoryStatusEnum.InProgress,
            Rating = Rating.E,
            ExternalLinks = [Ao3()]
        };
        _chapterReadService.ChapterList =
        [
            new ChapterListEntryDto(101, 1, "Chapter One", 100, true, null, false, 0f, [])
        ];

        IRenderedComponent<StoryPage> cut = Render<StoryPage>(p => p
            .Add(c => c.StoryId, 5));

        int chaptersIndex = cut.Markup.IndexOf("Chapters", StringComparison.Ordinal);
        int linksIndex = cut.Markup.IndexOf("Also posted on:", StringComparison.Ordinal);
        int recsIndex = cut.Markup.IndexOf("Recommendations", StringComparison.Ordinal);

        linksIndex.Should().BeGreaterThan(chaptersIndex,
            "settled placement: after the chapter list — a meaningful feature, not a mission-level one");
        recsIndex.Should().BeGreaterThan(linksIndex, "and before the recommendations section");
    }
}
