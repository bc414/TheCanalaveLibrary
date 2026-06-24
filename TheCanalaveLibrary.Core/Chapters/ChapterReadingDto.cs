namespace TheCanalaveLibrary.Core;

/// <summary>
/// Full payload for the chapter reading page. Bundles prev/next chapter numbers and the story's
/// rating so the L3 logic can render the "chapter rating exceeds story rating → skip to next"
/// warning without a second service call.
/// </summary>
public record ChapterReadingDto(
    int ChapterId,
    int StoryId,
    int ChapterNumber,
    string Title,
    /// <summary>Sanitized HTML — trusted stored content; never re-sanitize on display.</summary>
    string ChapterText,
    string? TopAuthorsNote,
    string? BottomAuthorsNote,
    int WordCount,
    /// <summary>
    /// Effective rating — resolved as <c>RawRating ?? StoryRating</c>. Always non-nullable;
    /// used by the reading page for warning display.
    /// </summary>
    Rating Rating,
    int? AuthorId,
    string AuthorName,
    /// <summary>The <c>SortOrder</c> of the version being displayed (0 = primary).</summary>
    int VersionOrder,
    string? VersionName,
    DateTime PublishDate,
    int? PreviousChapterNumber,
    int? NextChapterNumber,
    /// <summary>
    /// The parent story's rating — used by the reader page to decide whether to show a
    /// content-rating warning when this chapter's rating exceeds the story's ceiling.
    /// </summary>
    Rating StoryRating,
    /// <summary>
    /// Raw nullable rating from the DB — null means this version inherits the story's rating.
    /// Populated only by <see cref="IChapterReadService.GetChapterForEditAsync"/> for the
    /// edit-form's "Same as story" vs explicit-override display. Null on reading-page loads.
    /// </summary>
    Rating? RawRating = null
);
