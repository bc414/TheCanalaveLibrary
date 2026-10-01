namespace TheCanalaveLibrary.Core;

/// <summary>
/// One row in the story landing page's chapter list (WU25 <c>ChapterList</c> leaf; enriched in
/// WU45 with per-viewer read state for the stateful chapter list). Distinct from
/// <see cref="ChapterTocEntryDto"/> — that is a lean entry for the reading-page dropdown
/// (<c>ChapterNavigation</c>); this carries the viewer-accessible non-primary versions plus the
/// viewer's Feature-44 read state so the landing page can render progress fill-bars, read marks,
/// and the frontier-window collapse without a per-chapter round-trip.
///
/// <see cref="AlternateVersions"/> holds only non-primary versions accessible to the current
/// viewer's <c>ShowMatureContent</c> ceiling. Empty for the common single-version case (≈95%).
/// The primary version is represented by the main row itself — it is never included in
/// <see cref="AlternateVersions"/>.
///
/// <para><b>Per-viewer fields (WU45):</b> <see cref="IsRead"/>/<see cref="ReadProgress"/> come
/// from the viewer's <c>UserChapterInteraction</c> row (false/0 when absent or anonymous).
/// <see cref="PublishDate"/> is the chapter's publish anchor, <c>Chapter.FirstPublishedDate</c> —
/// stamped on the chapter's first publish and never moved, so promoting an alternate version never
/// changes it (owner ruling D2, WU-StoryLifecycle; the name is kept to limit ripple). Null for a
/// never-published draft. The "New" badge input (strict chain rule computed in
/// <c>ChapterListSegmenter</c>, not stored here).</para>
/// </summary>
public record ChapterListEntryDto(
    int ChapterId,
    int ChapterNumber,
    string Title,
    int WordCount,
    bool IsPublished,
    DateTime? PublishDate,
    bool IsRead,
    float ReadProgress,
    IReadOnlyList<ChapterVersionDto> AlternateVersions
);
