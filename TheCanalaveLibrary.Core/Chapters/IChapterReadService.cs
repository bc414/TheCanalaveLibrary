namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Chapters service contract. All methods return Core DTOs — EF entities never
/// cross this boundary (DTO Firewall, layer2-services.md).
/// </summary>
public interface IChapterReadService
{
    /// <summary>
    /// Returns the full reading payload for one chapter of a story.
    /// </summary>
    /// <param name="storyId">The parent story.</param>
    /// <param name="chapterNumber">1-based chapter number within the story.</param>
    /// <param name="versionOrder">
    /// The <c>SortOrder</c> of the desired version, or <c>null</c> to use the primary version
    /// (<c>Chapter.PrimaryContentId</c>). The version must be accessible under the current
    /// user's <c>ShowMatureContent</c> ceiling.
    /// </param>
    /// <returns>
    /// The chapter reading DTO, or <c>null</c> when the chapter / story does not exist, the
    /// chapter is not published, or the requested version is inaccessible.
    /// </returns>
    Task<ChapterReadingDto?> GetChapterForReadingAsync(
        int storyId,
        int chapterNumber,
        int? versionOrder = null);

    /// <summary>
    /// Returns the ordered table-of-contents for a story — all chapters, ordered by
    /// <c>ChapterNumber</c>. Includes a flag indicating whether each chapter has alternate
    /// versions accessible to the current viewer.
    /// </summary>
    Task<IReadOnlyList<ChapterTocEntryDto>> GetChapterTocAsync(int storyId);

    /// <summary>
    /// Returns the list of versions for a single chapter that the current viewer is permitted
    /// to see, ordered by <c>SortOrder</c>.
    /// </summary>
    Task<IReadOnlyList<ChapterVersionDto>> GetChapterVersionsAsync(int storyId, int chapterNumber);

    /// <summary>
    /// Returns the full chapter list for a story's landing page — all chapters (ordered by
    /// <c>ChapterNumber</c>), each with their viewer-accessible non-primary alternate versions.
    /// Used by <c>StoryPage</c> (WU25) to feed the <c>ChapterList</c> leaf; distinct from
    /// <see cref="GetChapterTocAsync"/> which is the lean dropdown TOC for the reading-page
    /// <c>ChapterNavigation</c> composite.
    /// <see cref="ChapterListEntryDto.AlternateVersions"/> holds non-primary versions only
    /// (empty for the common single-version case); the primary version is the main row.
    /// </summary>
    Task<IReadOnlyList<ChapterListEntryDto>> GetChapterListAsync(int storyId);

    /// <summary>
    /// The viewer's most recent <c>UserChapterInteraction.LastInteractionDate</c> across all
    /// chapters of a story — the "New"-badge watermark (WU45). <c>null</c> for anonymous viewers
    /// or when the viewer has never interacted with the story (which suppresses all New badges
    /// per the strict chain rule; see <c>ChapterListSegmenter</c>).
    /// </summary>
    Task<DateTime?> GetViewerLastInteractionUtcAsync(int storyId);

    /// <summary>
    /// Loads a specific <c>ChapterContent</c> row for the author's edit form.
    /// Does not apply the <c>ShowMatureContent</c> ceiling (authors see their own versions
    /// regardless of rating).
    /// </summary>
    Task<ChapterReadingDto?> GetChapterForEditAsync(long chapterContentId);

    /// <summary>
    /// Returns every published chapter's primary-version content for story export (WU38c),
    /// ordered by <c>ChapterNumber</c>, in one query. Applies the viewer's
    /// <c>ShowMatureContent</c> ceiling like the reading paths ("export = what you can read").
    /// Alternate versions are deliberately excluded — exports carry the canonical text.
    /// </summary>
    Task<IReadOnlyList<ChapterExportDto>> GetChaptersForExportAsync(int storyId);
}
