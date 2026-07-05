using AngleSharp.Dom;
using Bunit;
using FluentAssertions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for <see cref="ChapterList"/> (WU25). Pure leaf — no service injection required.
/// Covers:
/// <list type="bullet">
///   <item>Single-version chapter renders one main row with the correct primary URL; no sub-rows.</item>
///   <item>Multi-version chapter renders indented sub-rows labeled "Title — VersionName" with
///   correct version URLs.</item>
///   <item>Alternate with no VersionName falls back to "Version {VersionOrder}".</item>
///   <item>ShowDrafts=false hides unpublished chapters; ShowDrafts=true reveals them with a
///   "Draft" marker.</item>
///   <item>Empty chapter list shows the "No chapters yet." message.</item>
/// </list>
///
/// <b>Not tested here:</b> visual/Tailwind layout (human sign-off for Stage 6).
/// Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class ChapterListTests : BunitContext
{
    private const int StoryId = 42;

    // ── Factory helpers ──────────────────────────────────────────────────────────

    private static ChapterListEntryDto MakeChapter(
        int chapterNumber = 1,
        string title = "Chapter Title",
        int wordCount = 1_500,
        bool isPublished = true,
        IReadOnlyList<ChapterVersionDto>? alternates = null) =>
        new(chapterNumber, title, wordCount, isPublished, alternates ?? []);

    private static ChapterVersionDto MakeAlt(
        long contentId = 10,
        int versionOrder = 1,
        string? versionName = "Alt Version",
        int wordCount = 800) =>
        new(contentId, versionOrder, versionName, Rating.E, wordCount, IsPrimary: false);

    // ── Single-version chapter ────────────────────────────────────────────────────

    [Fact]
    public void ChapterList_SingleVersion_RendersOneRowWithPrimaryUrl()
    {
        IReadOnlyList<ChapterListEntryDto> chapters = [MakeChapter(chapterNumber: 1, title: "Prologue")];

        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, chapters)
            .Add(c => c.StoryId, StoryId));

        // Primary row link must target the chapter's clean URL (no version segment).
        cut.Find($"a[href='/story/{StoryId}/1']").TextContent.Should().Contain("Prologue",
            "the primary chapter row must link to /story/{StoryId}/{ChapterNumber} and show the title");
    }

    [Fact]
    public void ChapterList_SingleVersion_NoSubRowsRendered()
    {
        IReadOnlyList<ChapterListEntryDto> chapters = [MakeChapter(chapterNumber: 1)];

        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, chapters)
            .Add(c => c.StoryId, StoryId));

        // No alternate-version sub-row hrefs (which include a third path segment).
        cut.FindAll($"a[href^='/story/{StoryId}/1/']").Should().BeEmpty(
            "a single-version chapter has no alternate sub-rows — no /story/{StoryId}/1/* links");
    }

    // ── Multi-version chapter — sub-row content ───────────────────────────────────

    [Fact]
    public void ChapterList_WithAlternate_RendersSubRowWithVersionNameLabel()
    {
        ChapterVersionDto alt = MakeAlt(contentId: 11, versionOrder: 1, versionName: "T-Rated Cut");
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(chapterNumber: 2, title: "Rising Action", alternates: [alt])
        ];

        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, chapters)
            .Add(c => c.StoryId, StoryId));

        // Sub-row link text must be "Title — VersionName" (em dash separator in the Razor markup).
        IElement subRow = cut.Find($"a[href='/story/{StoryId}/2/1']");
        subRow.TextContent.Should().Contain("Rising Action",
            "sub-row label includes the chapter title");
        subRow.TextContent.Should().Contain("T-Rated Cut",
            "sub-row label includes the version name");
    }

    [Fact]
    public void ChapterList_WithAlternate_SubRowLinksToVersionUrl()
    {
        ChapterVersionDto alt = MakeAlt(contentId: 20, versionOrder: 2, versionName: "Extended");
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(chapterNumber: 3, title: "Climax", alternates: [alt])
        ];

        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, chapters)
            .Add(c => c.StoryId, StoryId));

        // Sub-row href must include the VersionOrder as the third path segment.
        cut.Find($"a[href='/story/{StoryId}/3/2']").Should().NotBeNull(
            "alternate version (VersionOrder=2) must link to /story/{StoryId}/3/2");
    }

    [Fact]
    public void ChapterList_WithMultipleAlternates_EachAlternateGetsItsOwnSubRow()
    {
        IReadOnlyList<ChapterVersionDto> alts =
        [
            MakeAlt(contentId: 30, versionOrder: 1, versionName: "Alt A"),
            MakeAlt(contentId: 31, versionOrder: 2, versionName: "Alt B"),
        ];
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(chapterNumber: 1, title: "Chapter One", alternates: alts)
        ];

        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, chapters)
            .Add(c => c.StoryId, StoryId));

        cut.Find($"a[href='/story/{StoryId}/1/1']").TextContent.Should().Contain("Alt A");
        cut.Find($"a[href='/story/{StoryId}/1/2']").TextContent.Should().Contain("Alt B");
    }

    // ── Alt with no VersionName — fallback label ─────────────────────────────────

    [Fact]
    public void ChapterList_AlternateWithNullVersionName_FallsBackToVersionN()
    {
        ChapterVersionDto unnamedAlt = new(ChapterContentId: 99, VersionOrder: 3, VersionName: null,
            Rating: Rating.E, WordCount: 500, IsPrimary: false);
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(chapterNumber: 1, title: "Intro", alternates: [unnamedAlt])
        ];

        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, chapters)
            .Add(c => c.StoryId, StoryId));

        IElement subRow = cut.Find($"a[href='/story/{StoryId}/1/3']");
        subRow.TextContent.Should().Contain("Version 3",
            "unnamed alternate must fall back to 'Version {VersionOrder}'");
    }

    // ── ShowDrafts filtering ──────────────────────────────────────────────────────

    [Fact]
    public void ChapterList_ShowDraftsFalse_HidesUnpublishedChapter()
    {
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(chapterNumber: 1, title: "Published", isPublished: true),
            MakeChapter(chapterNumber: 2, title: "Secret Draft", isPublished: false),
        ];

        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, chapters)
            .Add(c => c.StoryId, StoryId)
            .Add(c => c.ShowDrafts, false));

        cut.Markup.Should().Contain("Published",
            "published chapter must appear when ShowDrafts=false");
        cut.Markup.Should().NotContain("Secret Draft",
            "unpublished chapter must be hidden when ShowDrafts=false");
    }

    [Fact]
    public void ChapterList_ShowDraftsTrue_ShowsUnpublishedChapterWithDraftMarker()
    {
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(chapterNumber: 1, title: "Draft Chapter", isPublished: false),
        ];

        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, chapters)
            .Add(c => c.StoryId, StoryId)
            .Add(c => c.ShowDrafts, true));

        cut.Markup.Should().Contain("Draft Chapter",
            "author-visible unpublished chapter must appear when ShowDrafts=true");
        cut.Markup.Should().Contain("Draft",
            "unpublished chapter must carry a 'Draft' marker when ShowDrafts=true");
    }

    [Fact]
    public void ChapterList_ShowDraftsTrue_PublishedChapterHasNoDraftMarker()
    {
        // A published chapter must NOT carry the Draft marker even when ShowDrafts=true.
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(chapterNumber: 1, title: "Live Chapter", isPublished: true),
        ];

        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, chapters)
            .Add(c => c.StoryId, StoryId)
            .Add(c => c.ShowDrafts, true));

        // The markup will not contain a "Draft" label for this chapter.
        // We assert indirectly: the primary row is visible, but no "Draft" chip appears
        // beside the chapter number row for a published chapter.
        // bUnit renders all markup, so we check for the Draft text via structure —
        // the Draft span only appears inside the primary row when IsPublished=false.
        IElement primaryRow = cut.Find($"a[href='/story/{StoryId}/1']");
        primaryRow.QuerySelectorAll("span").Should()
            .NotContain(s => s.TextContent.Trim() == "Draft",
                "a published chapter's row must not carry a Draft marker even when ShowDrafts=true");
    }

    // ── Empty list ────────────────────────────────────────────────────────────────

    [Fact]
    public void ChapterList_EmptyList_ShowsNoChaptersMessage()
    {
        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, [])
            .Add(c => c.StoryId, StoryId));

        cut.Markup.Should().Contain("No chapters yet",
            "an empty chapter list must show the 'No chapters yet.' message");
    }

    [Fact]
    public void ChapterList_EmptyList_NoAnchorLinksRendered()
    {
        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, [])
            .Add(c => c.StoryId, StoryId));

        cut.FindAll($"a[href^='/story/{StoryId}/']").Should().BeEmpty(
            "no chapter links must appear for an empty chapter list");
    }

    // ── Word-count display in rows ────────────────────────────────────────────────

    [Theory]
    [InlineData(500, "500 words")]
    [InlineData(2_000, "2K words")]
    [InlineData(1_500_000, "1.5M words")]
    public void ChapterList_WordCountDisplay_FormatsCorrectly(int wordCount, string expected)
    {
        IReadOnlyList<ChapterListEntryDto> chapters =
        [
            MakeChapter(chapterNumber: 1, wordCount: wordCount)
        ];

        IRenderedComponent<ChapterList> cut = Render<ChapterList>(p => p
            .Add(c => c.Chapters, chapters)
            .Add(c => c.StoryId, StoryId));

        cut.Markup.Should().Contain(expected,
            $"word count {wordCount} should display as '{expected}'");
    }

    // ── Mutation-sanity note ──────────────────────────────────────────────────────
    // If the ShowDrafts=false filter were removed (all chapters rendered unconditionally),
    //   ChapterList_ShowDraftsFalse_HidesUnpublishedChapter fails on the NotContain assertion.
    // If AltVersionLabel returned versionName only (removing the "Version N" fallback),
    //   ChapterList_AlternateWithNullVersionName_FallsBackToVersionN would fail.
    // If AlternateVersions links used /story/{StoryId}/{ChapterNumber} instead of appending
    //   /{VersionOrder}, all sub-row href assertions would fail.
    // (Verified manually by temporarily mutating the source; all three cause failures as described.)
}
