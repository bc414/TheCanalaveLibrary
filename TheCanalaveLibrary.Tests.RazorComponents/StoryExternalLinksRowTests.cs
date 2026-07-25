using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for the "Also posted on" links row (Feature 53, WU39 display model, settled
/// 2026-07-24, audit/Moderation.md F53): a reviewed link (<c>IsReviewed</c>) shows a muted
/// "reviewed · author's account: &lt;handle&gt;" sub-line linking to the confirmed profile —
/// deliberately NO checkmark. A non-reviewed link renders as a plain single line with no sub-line
/// and no label — never-requested/pending/rejected are indistinguishable to the reader by design
/// (the reader DTO collapses them to a single <c>IsReviewed = false</c> shape; reporting is driven
/// by a reader's own outside knowledge, never by this internal state). Also the settled placement:
/// on the story page it sits after the chapter section and before recommendations. Tier:
/// RazorComponents (bUnit).
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

    private static StoryExternalLinkDto Ao3Reviewed() =>
        new("Archive of Our Own", "https://archiveofourown.org/works/123", true,
            "gengarlover", "https://archiveofourown.org/users/gengarlover");

    private static StoryExternalLinkDto FfnNotReviewed() =>
        new("FanFiction.Net", "https://www.fanfiction.net/s/456", false, null, null);

    // ── Leaf behavior ────────────────────────────────────────────────────────────

    [Fact]
    public void RendersNothing_WhenNoLinks()
    {
        IRenderedComponent<StoryExternalLinksRow> cut = Render<StoryExternalLinksRow>(p => p
            .Add(c => c.Links, (IReadOnlyList<StoryExternalLinkDto>)[]));

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void ReviewedLink_ShowsAccountSubLine_NoCheckmark()
    {
        IRenderedComponent<StoryExternalLinksRow> cut = Render<StoryExternalLinksRow>(p => p
            .Add(c => c.Links, (IReadOnlyList<StoryExternalLinkDto>)[Ao3Reviewed()]));

        cut.Markup.Should().Contain("Also posted on");
        cut.Markup.Should().Contain("reviewed");
        cut.Markup.Should().Contain("author's account");
        cut.Markup.Should().Contain("gengarlover");
        // Deliberately no checkmark glyph or "Author verified" seal language (settled 2026-07-24).
        cut.Markup.Should().NotContain("✓");
        cut.Markup.Should().NotContain("Author verified");

        var anchors = cut.FindAll("a");
        anchors.Should().HaveCount(2, "the story link and the confirmed-account link");
        anchors[0].GetAttribute("href").Should().Be("https://archiveofourown.org/works/123");
        anchors[1].GetAttribute("href").Should().Be("https://archiveofourown.org/users/gengarlover");
        anchors.Should().OnlyContain(a => a.GetAttribute("target") == "_blank");
        anchors.Should().OnlyContain(a => a.GetAttribute("rel")!.Contains("nofollow"));
    }

    [Fact]
    public void NonReviewedLink_RendersPlainSingleLine_NoSubLineNoLabel()
    {
        // Covers never-requested / pending / rejected alike — all collapse to IsReviewed = false
        // on the reader DTO by design (visually indistinguishable, settled 2026-07-24).
        IRenderedComponent<StoryExternalLinksRow> cut = Render<StoryExternalLinksRow>(p => p
            .Add(c => c.Links, (IReadOnlyList<StoryExternalLinkDto>)[FfnNotReviewed()]));

        var anchors = cut.FindAll("a");
        anchors.Should().ContainSingle("a non-reviewed link has no companion account link");
        anchors[0].GetAttribute("href").Should().Be("https://www.fanfiction.net/s/456");
        anchors[0].GetAttribute("target").Should().Be("_blank");
        anchors[0].GetAttribute("rel").Should().Contain("nofollow");

        cut.Markup.Should().NotContain("reviewed");
        cut.Markup.Should().NotContain("author's account");
    }

    [Fact]
    public void MixOfReviewedAndNonReviewed_EachRendersItsOwnShape()
    {
        IRenderedComponent<StoryExternalLinksRow> cut = Render<StoryExternalLinksRow>(p => p
            .Add(c => c.Links, (IReadOnlyList<StoryExternalLinkDto>)[Ao3Reviewed(), FfnNotReviewed()]));

        cut.FindAll("a").Should().HaveCount(3, "reviewed link's two anchors + the plain link's one");
        cut.Markup.Should().Contain("gengarlover");
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
            ExternalLinks = [FfnNotReviewed()]
        };
        _chapterReadService.ChapterList =
        [
            new ChapterListEntryDto(101, 1, "Chapter One", 100, true, null, false, 0f, [])
        ];

        IRenderedComponent<StoryPage> cut = Render<StoryPage>(p => p
            .Add(c => c.StoryId, 5));

        int chaptersIndex = cut.Markup.IndexOf("Chapters", StringComparison.Ordinal);
        int linksIndex = cut.Markup.IndexOf("Also posted on", StringComparison.Ordinal);
        int recsIndex = cut.Markup.IndexOf("Recommendations", StringComparison.Ordinal);

        linksIndex.Should().BeGreaterThan(chaptersIndex,
            "settled placement: after the chapter list — a meaningful feature, not a mission-level one");
        recsIndex.Should().BeGreaterThan(linksIndex, "and before the recommendations section");
    }
}
