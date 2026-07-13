using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ChapterListSegmenter"/> (WU45) — the pure shared boundary scan.
/// Covers: the no-arc frontier-windowing cases (zero-read, partially-read, fully-read,
/// under-threshold), read-run vs mixed-run expander labeling, the strict-chain "New" badge
/// (contiguous fresh run at the frontier only; broken chain and no-watermark suppression),
/// arc segments (sticky header even when fully read, frontier arc expanded by default, no
/// windowing inside arcs, zero-visible-chapter arcs skipped, computed ordinals), and the
/// arcs-govern-the-tail rule (TailWindow only for gap tails). Tier: Unit — direct construction,
/// no host/DB (testing.md), exactly why the segmenter lives dependency-free in Core.
/// </summary>
public class ChapterListSegmenterTests
{
    // Small tunables so tests stay compact — also proves the constants are genuinely tunable.
    private static readonly ChapterListCollapseOptions Opt =
        new(CollapseMinimum: 5, HeadWindow: 2, TailWindow: 2);

    private static readonly DateTime Watermark = new(2026, 07, 01, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OldDate   = Watermark.AddDays(-10);
    private static readonly DateTime FreshDate = Watermark.AddDays(+1);

    private static ChapterListEntryDto Ch(
        int number, bool isRead = false, float progress = 0f, DateTime? published = null) =>
        new(ChapterId: 1000 + number, number, $"Chapter {number}", 1_000, IsPublished: true,
            PublishDate: published ?? OldDate, IsRead: isRead, ReadProgress: progress,
            AlternateVersions: []);

    private static List<ChapterListEntryDto> Chapters(int count, int readThrough = 0) =>
        [.. Enumerable.Range(1, count).Select(n => Ch(n, isRead: n <= readThrough))];

    private static StoryArcDto Arc(int id, string title, int start, int end) =>
        new(id, title, start, end);

    // ── Degenerate / threshold ────────────────────────────────────────────────────

    [Fact]
    public void EmptyList_ReturnsEmpty() =>
        ChapterListSegmenter.Build([], [], null, Opt).Should().BeEmpty();

    [Fact]
    public void UnderCollapseMinimum_AllRowsInline_NoExpanders()
    {
        var items = ChapterListSegmenter.Build(Chapters(4), [], null, Opt);

        items.Should().HaveCount(4).And.AllBeOfType<ChapterListRowItem>(
            "segments shorter than CollapseMinimum never collapse");
    }

    // ── No-arc frontier windowing ─────────────────────────────────────────────────

    [Fact]
    public void ZeroRead_LongStory_ShowsHeadAndTail_CollapsesMiddle()
    {
        // 10 unread chapters, head=2, tail=2 → rows 1,2 | run 3..8 | rows 9,10.
        var items = ChapterListSegmenter.Build(Chapters(10), [], null, Opt);

        items.Should().HaveCount(5);
        VisibleNumbers(items).Should().Equal(1, 2, 9, 10);
        var run = items.OfType<ChapterListCollapsedRunItem>().Single();
        run.Count.Should().Be(6);
        run.IsReadRun.Should().BeFalse("the hidden middle is unread — plain 'chapters hidden'");
        run.HiddenRows.Select(r => r.Entry.ChapterNumber).Should().Equal(3, 4, 5, 6, 7, 8);
    }

    [Fact]
    public void PartiallyRead_CollapsesReadRun_WindowsAtFrontier()
    {
        // 12 chapters, 1..6 read → frontier at 7: run[1..6](read) | rows 7,8 | run[9,10] | rows 11,12.
        var items = ChapterListSegmenter.Build(Chapters(12, readThrough: 6), [], null, Opt);

        VisibleNumbers(items).Should().Equal(7, 8, 11, 12);
        var runs = items.OfType<ChapterListCollapsedRunItem>().ToList();
        runs.Should().HaveCount(2);
        runs[0].IsReadRun.Should().BeTrue("everything behind the frontier is read");
        runs[0].HiddenRows.Select(r => r.Entry.ChapterNumber).Should().Equal(1, 2, 3, 4, 5, 6);
        runs[1].IsReadRun.Should().BeFalse();
        runs[1].HiddenRows.Select(r => r.Entry.ChapterNumber).Should().Equal(9, 10);
    }

    [Fact]
    public void InProgressChapter_IsTheFrontier()
    {
        // Chapter 3 at 50% (not IsRead) — the window must start there, not after it.
        List<ChapterListEntryDto> chapters =
            [Ch(1, isRead: true), Ch(2, isRead: true), Ch(3, progress: 0.5f),
             Ch(4), Ch(5), Ch(6), Ch(7), Ch(8)];

        var items = ChapterListSegmenter.Build(chapters, [], null, Opt);

        VisibleNumbers(items).Should().StartWith([3, 4],
            "the frontier is the first NOT-fully-read chapter — in-progress counts");
    }

    [Fact]
    public void FullyRead_CollapsesEverything_ExceptGapTail()
    {
        var items = ChapterListSegmenter.Build(Chapters(10, readThrough: 10), [], null, Opt);

        VisibleNumbers(items).Should().Equal([9, 10], "no frontier window exists past the end; "
            + "the gap-tail TailWindow still keeps the last chapters reachable");
        var run = items.OfType<ChapterListCollapsedRunItem>().Single();
        run.IsReadRun.Should().BeTrue();
        run.Count.Should().Be(8);
    }

    [Fact]
    public void RevealKey_IsStable_AcrossResegmentation()
    {
        var before = ChapterListSegmenter.Build(Chapters(12, readThrough: 6), [], null, Opt);
        // Reader marks chapter 12 read (out-of-order) — re-run the same pure function.
        List<ChapterListEntryDto> after12 = [.. Chapters(12, readThrough: 6)];
        after12[11] = after12[11] with { IsRead = true, ReadProgress = 1f };
        var after = ChapterListSegmenter.Build(after12, [], null, Opt);

        string beforeKey = before.OfType<ChapterListCollapsedRunItem>().First().Key;
        string afterKey  = after.OfType<ChapterListCollapsedRunItem>().First().Key;
        afterKey.Should().Be(beforeKey,
            "run keys anchor to the first hidden chapter number so revealed state survives");
    }

    // ── "New" badge — strict chain rule ──────────────────────────────────────────

    [Fact]
    public void New_CaughtUpReader_BadgesTheContiguousFreshRun()
    {
        // All read except two freshly-published chapters at the end.
        List<ChapterListEntryDto> chapters =
            [Ch(1, isRead: true), Ch(2, isRead: true), Ch(3, isRead: true),
             Ch(4, published: FreshDate), Ch(5, published: FreshDate)];

        var items = ChapterListSegmenter.Build(chapters, [], Watermark, Opt);

        NewNumbers(items).Should().Equal([4, 5],
            "the chain runs through the fresh block itself — both post-watermark chapters badge");
    }

    [Fact]
    public void New_HalfReadReader_GetsNoBadges()
    {
        // Reader stopped at 2; chapters 3..4 old-unread; 5 freshly published.
        List<ChapterListEntryDto> chapters =
            [Ch(1, isRead: true), Ch(2, isRead: true), Ch(3), Ch(4), Ch(5, published: FreshDate)];

        var items = ChapterListSegmenter.Build(chapters, [], Watermark, Opt);

        NewNumbers(items).Should().BeEmpty(
            "an unread pre-existing chapter breaks the chain — the fresh chapter is just as "
            + "unknown as the old unread ones (WU45 settled rationale)");
    }

    [Fact]
    public void New_NoWatermark_SuppressesAllBadges()
    {
        List<ChapterListEntryDto> chapters =
            [Ch(1, isRead: true), Ch(2, published: FreshDate)];

        var items = ChapterListSegmenter.Build(chapters, [], viewerWatermarkUtc: null, Opt);

        NewNumbers(items).Should().BeEmpty("anonymous / first-visit viewers have no watermark");
    }

    [Fact]
    public void New_ChainStopsAtFirstNonFreshChapter()
    {
        // Caught up; fresh, fresh, OLD-unread, fresh — the old one ends the chain.
        List<ChapterListEntryDto> chapters =
            [Ch(1, isRead: true),
             Ch(2, published: FreshDate), Ch(3, published: FreshDate),
             Ch(4, published: OldDate), Ch(5, published: FreshDate)];

        var items = ChapterListSegmenter.Build(chapters, [], Watermark, Opt);

        NewNumbers(items).Should().Equal(2, 3);
    }

    // ── Arcs ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void ArcSegment_EmitsHeaderPlusAllRows_NoWindowingInside()
    {
        // 12 unread chapters all inside one arc — well past CollapseMinimum, yet no expanders.
        var items = ChapterListSegmenter.Build(
            Chapters(12), [Arc(1, "Book 1", 1, 12)], null, Opt);

        items.OfType<ChapterListCollapsedRunItem>().Should().BeEmpty(
            "arcs supersede frontier-windowing for the chapters they cover");
        var header = items.OfType<ChapterListArcHeaderItem>().Single();
        (header.Title, header.Ordinal, header.ChapterCount, header.ReadCount)
            .Should().Be(("Book 1", 1, 12, 0));
        header.DefaultCollapsed.Should().BeFalse("the frontier (ch.1) is inside this arc");
        items.OfType<ChapterListRowItem>().Should().HaveCount(12)
            .And.OnlyContain(r => r.StoryArcId == 1, "rows are tagged for arc-collapse skipping");
    }

    [Fact]
    public void FrontierArc_Expanded_OtherArcsCollapsed_ByDefault()
    {
        // Arc1 ch1-4 fully read; arc2 ch5-8 unread → frontier ch5 is in arc2.
        var items = ChapterListSegmenter.Build(
            Chapters(8, readThrough: 4),
            [Arc(1, "Book 1", 1, 4), Arc(2, "Book 2", 5, 8)], null, Opt);

        var headers = items.OfType<ChapterListArcHeaderItem>().ToList();
        headers.Should().HaveCount(2, "arc headers are sticky — a fully-read arc still shows");
        headers[0].DefaultCollapsed.Should().BeTrue("not the frontier arc");
        headers[0].ReadCount.Should().Be(4);
        headers[1].DefaultCollapsed.Should().BeFalse("contains the frontier");
        headers[1].Ordinal.Should().Be(2, "ordinal = position by StartChapterNumber, computed");
    }

    [Fact]
    public void GapChapters_BetweenArcs_RenderAsPlainRows()
    {
        // Prologue (1), arc 2-4, interlude (5), arc 6-8 — gaps are bare rows, no synthetic arc.
        var items = ChapterListSegmenter.Build(
            Chapters(8),
            [Arc(1, "Book 1", 2, 4), Arc(2, "Book 2", 6, 8)], null, Opt);

        var seq = items.Select(i => i switch
        {
            ChapterListArcHeaderItem h => $"H{h.Ordinal}",
            ChapterListRowItem r => r.StoryArcId is null ? $"gap{r.Entry.ChapterNumber}" : $"c{r.Entry.ChapterNumber}",
            _ => "run"
        });
        seq.Should().Equal(
            "gap1", "H1", "c2", "c3", "c4", "gap5", "H2", "c6", "c7", "c8");
    }

    [Fact]
    public void Arc_CoveringNoVisibleChapter_EmitsNothing()
    {
        // Arc planned ahead of the written story (range 5-8, only 3 chapters exist).
        var items = ChapterListSegmenter.Build(
            Chapters(3), [Arc(1, "Future Book", 5, 8)], null, Opt);

        items.OfType<ChapterListArcHeaderItem>().Should().BeEmpty(
            "an arc with no visible chapters has nothing to govern — no orphan header");
    }

    [Fact]
    public void StoryEndingInArc_GetsNoTailWindow_ArcGoverns()
    {
        // 14 read chapters: gap 1-6, arc 7-14 (all read). Frontier past end.
        var items = ChapterListSegmenter.Build(
            Chapters(14, readThrough: 14), [Arc(1, "Finale", 7, 14)], null, Opt);

        // The gap segment (1-6) is fully read and NOT the story tail → one read-run, no rows.
        var gapRun = items.OfType<ChapterListCollapsedRunItem>().Single();
        gapRun.Count.Should().Be(6);
        gapRun.IsReadRun.Should().BeTrue();
        // The arc gets its sticky header (collapsed — no frontier) and its rows; the TailWindow
        // does NOT force the last chapters visible — arcs fully govern their region (settled).
        var header = items.OfType<ChapterListArcHeaderItem>().Single();
        header.DefaultCollapsed.Should().BeTrue();
        items.OfType<ChapterListRowItem>().Should().OnlyContain(r => r.StoryArcId == 1);
    }

    [Fact]
    public void GapTailAfterLastArc_GetsTheTailWindow()
    {
        // Arc 1-4 read; long unread gap tail 5-16 → head window at 5,6 + tail 15,16.
        var items = ChapterListSegmenter.Build(
            Chapters(16, readThrough: 4), [Arc(1, "Book 1", 1, 4)], null, Opt);

        VisibleNumbers(items).Should().Equal([5, 6, 15, 16],
            "the story tail IS a gap segment here, so the TailWindow applies");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>Chapter numbers of directly-visible rows in GAP segments (arc rows excluded).</summary>
    private static List<int> VisibleNumbers(IReadOnlyList<ChapterListItem> items) =>
        [.. items.OfType<ChapterListRowItem>()
            .Where(r => r.StoryArcId is null)
            .Select(r => r.Entry.ChapterNumber)];

    private static List<int> NewNumbers(IReadOnlyList<ChapterListItem> items) =>
        [.. items.OfType<ChapterListRowItem>().Where(r => r.IsNew).Select(r => r.Entry.ChapterNumber)
            .Concat(items.OfType<ChapterListCollapsedRunItem>()
                .SelectMany(run => run.HiddenRows).Where(r => r.IsNew)
                .Select(r => r.Entry.ChapterNumber))
            .Order()];
}
