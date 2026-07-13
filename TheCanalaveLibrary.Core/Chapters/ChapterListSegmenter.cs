namespace TheCanalaveLibrary.Core;

/// <summary>
/// The WU45 chapter-list boundary scan — ONE pure function, deliberately dependency-free (no EF,
/// no JS, no host), so the SAME logic runs server-side for the initial SSR paint and client-side
/// to re-segment after a read-state mutation (InteractiveAuto: server circuit or WASM). Never
/// fork this logic per host — that drift is exactly what this placement prevents.
///
/// <para><b>The model (settled 2026-07-12, audit/Chapters.md "WU45 settled design"):</b> the
/// chapter list is one flat ordered sequence. Every chapter is governed by exactly one collapse
/// authority:</para>
/// <list type="bullet">
/// <item><b>Arc-governed</b> — collapse granularity is the whole arc: a sticky, toggleable header
/// (frontier arc expanded by default, others collapsed) and NO windowing inside an expanded arc.
/// Arcs are the author's hand-pruned digestibility mechanism and supersede auto-collapse for the
/// chapters they cover. An arc covering zero visible chapters emits nothing.</item>
/// <item><b>Frontier-windowed</b> (gap segments; the whole story in the ~95% no-arc case) —
/// read runs collapse behind counted expanders; a <c>HeadWindow</c> stays visible from the
/// frontier; the unread tail past the window collapses; a <c>TailWindow</c> keeps the story's
/// last chapters visible when (and only when) the story tail is itself a gap segment. Segments
/// shorter than <c>CollapseMinimum</c> never collapse.</item>
/// </list>
///
/// <para><b>"New" badge — strict chain rule:</b> a chapter is New iff its PublishDate is after
/// the viewer's watermark AND every earlier chapter is read or itself New (i.e., the contiguous
/// fresh run starting at the frontier). One unread pre-existing chapter before the run kills all
/// badges; no watermark (anonymous / first visit) means no badges. Cosmetic only — a New chapter
/// never pierces collapse (in practice the chain rule puts it at the frontier, which the head
/// window shows anyway).</para>
/// </summary>
public static class ChapterListSegmenter
{
    /// <param name="chapters">The rows that will render, in ChapterNumber order — the caller has
    /// already applied its visibility filter (readers: published only; authors: + drafts).</param>
    /// <param name="arcs">The story's arcs in reading order (may be empty — the ~95% case).</param>
    /// <param name="viewerWatermarkUtc">The viewer's most recent chapter interaction on this
    /// story (<c>IChapterReadService.GetViewerLastInteractionUtcAsync</c>); null suppresses all
    /// New badges.</param>
    /// <param name="options">Collapse tuning constants; tests pass custom values.</param>
    public static IReadOnlyList<ChapterListItem> Build(
        IReadOnlyList<ChapterListEntryDto> chapters,
        IReadOnlyList<StoryArcDto> arcs,
        DateTime? viewerWatermarkUtc,
        ChapterListCollapseOptions? options = null)
    {
        options ??= ChapterListCollapseOptions.Default;
        if (chapters.Count == 0) return [];

        // ── Frontier: index of the first not-fully-read chapter (count = fully read story). ──
        int frontier = 0;
        while (frontier < chapters.Count && chapters[frontier].IsRead) frontier++;

        // ── New badges: the contiguous post-watermark run starting exactly at the frontier. ──
        bool[] isNew = new bool[chapters.Count];
        if (viewerWatermarkUtc is DateTime watermark)
        {
            for (int i = frontier; i < chapters.Count; i++)
            {
                if (chapters[i].PublishDate is DateTime published && published > watermark)
                    isNew[i] = true;
                else
                    break; // one non-fresh chapter ends the chain — later fresh ones stay unbadged
            }
        }

        // ── Arc assignment per chapter + the frontier arc (expanded-by-default). ──
        int?[] arcIdByIndex = new int?[chapters.Count];
        for (int i = 0; i < chapters.Count; i++)
        {
            int number = chapters[i].ChapterNumber;
            foreach (StoryArcDto arc in arcs)
            {
                if (number < arc.StartChapterNumber) break; // arcs are ordered; no later arc matches
                if (number <= arc.EndChapterNumber) { arcIdByIndex[i] = arc.StoryArcId; break; }
            }
        }
        int? frontierArcId = frontier < chapters.Count ? arcIdByIndex[frontier] : null;

        // ── Walk once, emitting maximal same-arc-key segments. ──
        List<ChapterListItem> items = [];
        int segStart = 0;
        while (segStart < chapters.Count)
        {
            int? segArcId = arcIdByIndex[segStart];
            int segEnd = segStart;
            while (segEnd + 1 < chapters.Count && arcIdByIndex[segEnd + 1] == segArcId) segEnd++;

            if (segArcId is int arcId)
                EmitArcSegment(items, chapters, isNew, arcs, arcId, segStart, segEnd, frontierArcId);
            else
                EmitGapSegment(items, chapters, isNew, segStart, segEnd,
                    frontier, isStoryTail: segEnd == chapters.Count - 1, options);

            segStart = segEnd + 1;
        }
        return items;
    }

    /// <summary>Arc segment: sticky header + every row (no windowing inside an arc).</summary>
    private static void EmitArcSegment(
        List<ChapterListItem> items,
        IReadOnlyList<ChapterListEntryDto> chapters,
        bool[] isNew,
        IReadOnlyList<StoryArcDto> arcs,
        int arcId,
        int segStart,
        int segEnd,
        int? frontierArcId)
    {
        int ordinal = 0;
        string title = string.Empty;
        for (int a = 0; a < arcs.Count; a++)
        {
            if (arcs[a].StoryArcId != arcId) continue;
            ordinal = a + 1; // "Arc X" — computed position, never stored (SortOrder eliminated)
            title = arcs[a].Title;
            break;
        }

        int readCount = 0;
        for (int i = segStart; i <= segEnd; i++)
            if (chapters[i].IsRead) readCount++;

        items.Add(new ChapterListArcHeaderItem(
            arcId, title, ordinal,
            ChapterCount: segEnd - segStart + 1,
            ReadCount: readCount,
            DefaultCollapsed: frontierArcId != arcId));

        for (int i = segStart; i <= segEnd; i++)
            items.Add(new ChapterListRowItem(chapters[i], arcId, isNew[i]));
    }

    /// <summary>
    /// Gap segment: frontier windowing. Visible = the HeadWindow from the frontier (when the
    /// frontier falls in this segment) + the story-tail TailWindow (when this segment ends the
    /// story). Consecutive hidden chapters group into counted expander runs, labeled read-run
    /// when every hidden row in the run is read.
    /// </summary>
    private static void EmitGapSegment(
        List<ChapterListItem> items,
        IReadOnlyList<ChapterListEntryDto> chapters,
        bool[] isNew,
        int segStart,
        int segEnd,
        int frontier,
        bool isStoryTail,
        ChapterListCollapseOptions options)
    {
        int length = segEnd - segStart + 1;

        bool[] visible = new bool[length];
        if (length < options.CollapseMinimum)
        {
            Array.Fill(visible, true); // short segments never collapse
        }
        else
        {
            if (frontier >= segStart && frontier <= segEnd)
                for (int i = frontier; i <= Math.Min(segEnd, frontier + options.HeadWindow - 1); i++)
                    visible[i - segStart] = true;

            if (isStoryTail)
                for (int i = Math.Max(segStart, segEnd - options.TailWindow + 1); i <= segEnd; i++)
                    visible[i - segStart] = true;
        }

        int i2 = segStart;
        while (i2 <= segEnd)
        {
            if (visible[i2 - segStart])
            {
                items.Add(new ChapterListRowItem(chapters[i2], null, isNew[i2]));
                i2++;
                continue;
            }

            // Collect the maximal hidden run.
            List<ChapterListRowItem> hidden = [];
            bool allRead = true;
            while (i2 <= segEnd && !visible[i2 - segStart])
            {
                hidden.Add(new ChapterListRowItem(chapters[i2], null, isNew[i2]));
                allRead &= chapters[i2].IsRead;
                i2++;
            }
            items.Add(new ChapterListCollapsedRunItem(
                hidden, IsReadRun: allRead,
                Key: $"run-{hidden[0].Entry.ChapterNumber}"));
        }
    }
}
