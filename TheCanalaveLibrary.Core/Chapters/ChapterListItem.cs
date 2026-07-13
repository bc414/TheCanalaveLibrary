namespace TheCanalaveLibrary.Core;

/// <summary>
/// One item in the flat render-item list produced by <see cref="ChapterListSegmenter"/> (WU45).
/// The chapter list is ONE flat sequence — arc headers and collapse expanders are siblings of
/// chapter rows, not containers around them (validated against Fimfiction's own flat DOM; see
/// audit/Chapters.md "WU45 settled design"). The component walks this list in order and applies
/// its ephemeral view-state (collapsed arcs, revealed runs) at render time.
/// </summary>
public abstract record ChapterListItem;

/// <summary>
/// Sticky arc header (WU45): always emitted for an arc covering ≥1 visible chapter, even when
/// every chapter in it is read. <paramref name="DefaultCollapsed"/> encodes the frontier rule —
/// the arc containing the frontier chapter (first not-fully-read) starts expanded, every other
/// arc starts collapsed; the component may override per user toggle (ephemeral view-state).
/// <paramref name="Ordinal"/> is the computed "Arc X" position (1-based, by StartChapterNumber).
/// </summary>
public sealed record ChapterListArcHeaderItem(
    int StoryArcId,
    string Title,
    int Ordinal,
    int ChapterCount,
    int ReadCount,
    bool DefaultCollapsed) : ChapterListItem;

/// <summary>
/// A collapsed run of consecutive chapters in a gap (non-arc) segment — rendered as a counted
/// expander ("N read chapters hidden. Show" / "N chapters hidden. Show"). The hidden rows ride
/// inside the item so expansion is pure local view-state (no refetch, no re-walk).
/// <paramref name="Key"/> is stable across re-segmentation (first hidden chapter number) so a
/// revealed run stays revealed after a read-state mutation re-runs the segmenter.
/// </summary>
public sealed record ChapterListCollapsedRunItem(
    IReadOnlyList<ChapterListRowItem> HiddenRows,
    bool IsReadRun,
    string Key) : ChapterListItem
{
    public int Count => HiddenRows.Count;
}

/// <summary>
/// A visible chapter row. <paramref name="StoryArcId"/> tags rows inside an arc so the component
/// can skip them while that arc is collapsed (the flat-list equivalent of arc containment).
/// <paramref name="IsNew"/> is the strict-chain New badge (WU45): published after the viewer's
/// watermark AND every earlier chapter is read or itself New — computed here, never stored.
/// </summary>
public sealed record ChapterListRowItem(
    ChapterListEntryDto Entry,
    int? StoryArcId,
    bool IsNew) : ChapterListItem;

/// <summary>
/// The WU45 collapse tuning knobs — named, easily-tunable constants (deliberately NOT hardcoded
/// at call sites; the shipped numbers are acknowledged first-guesses to refine at visual review).
/// </summary>
/// <param name="CollapseMinimum">A gap segment shorter than this never collapses at all (~10).</param>
/// <param name="HeadWindow">Chapters kept visible starting at the frontier (first
/// in-progress-or-unread chapter; chapter 1 for a zero-read/anonymous viewer).</param>
/// <param name="TailWindow">Last-of-story chapters kept visible — ONLY when the story's tail is a
/// gap segment (arcs fully govern the regions they cover; WU45 settled).</param>
public sealed record ChapterListCollapseOptions(int CollapseMinimum, int HeadWindow, int TailWindow)
{
    public static readonly ChapterListCollapseOptions Default = new(CollapseMinimum: 10, HeadWindow: 3, TailWindow: 3);
}
