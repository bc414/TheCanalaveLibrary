namespace TheCanalaveLibrary.Core;

/// <summary>
/// One row in the story landing page's chapter list (WU25 <c>ChapterList</c> leaf).
/// Distinct from <see cref="ChapterTocEntryDto"/> — that is a lean entry for the reading-page
/// dropdown (<c>ChapterNavigation</c>); this carries the viewer-accessible non-primary versions
/// so the landing page can render them as indented sub-rows without a per-chapter round-trip.
///
/// <see cref="AlternateVersions"/> holds only non-primary versions accessible to the current
/// viewer's <c>ShowMatureContent</c> ceiling. Empty for the common single-version case (≈95%).
/// The primary version is represented by the main row itself — it is never included in
/// <see cref="AlternateVersions"/>.
/// </summary>
public record ChapterListEntryDto(
    int ChapterNumber,
    string Title,
    int WordCount,
    bool IsPublished,
    IReadOnlyList<ChapterVersionDto> AlternateVersions
);
