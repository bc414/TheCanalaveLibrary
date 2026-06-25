namespace TheCanalaveLibrary.Core;

/// <summary>
/// Contains all the details needed to display a story's landing page (StoryPage / WU25). Too dense
/// for search results (use <see cref="StoryListingDto"/> for listing surfaces); good for the full
/// story detail page.
///
/// <see cref="Tags"/> are sprite-resolved <see cref="TagChipDto"/>s — the producing read service
/// calls <c>ISpriteReadService.GetSpriteUrl</c> during projection (same discipline as listing service).
/// <see cref="CoverArtRelativeUrl"/> is a stored relative URL, used verbatim (never resolved through
/// a service — same discipline as <see cref="StoryListingDto.CoverArtRelativeUrl"/>).
/// <see cref="AuthorId"/> is nullable to handle anonymized/deleted-author stories.
/// </summary>
public class StoryDetailsDTO
{
    public int StoryId { get; set; }
    public string? StoryTitle { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public int WordCount { get; set; }
    public DateTime PublishDate { get; set; }
    public DateTime LastUpdatedDate { get; set; }
    public DateOnly? OriginalPublishDate { get; set; }
    public DateOnly? OriginalLastUpdatedDate { get; set; }
    public int? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? CoverArtRelativeUrl { get; set; }
    public Rating Rating { get; set; }
    public StoryStatusEnum Status { get; set; }
    public IReadOnlyList<TagChipDto> Tags { get; set; } = [];

    /// <summary>
    /// Legacy: chapter title list kept for backward-compatibility with the L5 JSON endpoint
    /// (<c>HttpStoryReadService</c> deserializes <c>StoryDetailsDTO</c>). The story landing page
    /// (WU25) uses <see cref="IChapterReadService.GetChapterListAsync"/> instead for richer
    /// per-chapter metadata. This field will be removed when the L5 endpoint is rebuilt (post-MVP).
    /// </summary>
    public List<string> ChapterNames { get; set; } = [];
}