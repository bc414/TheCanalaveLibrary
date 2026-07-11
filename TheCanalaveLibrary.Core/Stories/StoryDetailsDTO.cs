namespace TheCanalaveLibrary.Core;

/// <summary>
/// Contains all the details needed to display a story's landing page (StoryPage / WU25). Too dense
/// for search results (use <see cref="StoryListingDto"/> for listing surfaces); good for the full
/// story detail page.
///
/// <see cref="Tags"/> are sprite-resolved <see cref="TagChipDto"/>s (flat types: Genre, Setting,
/// ContentWarning, CrossoverFandom) plus character chips merged in from <see cref="Characters"/>.
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

    /// <summary>Flat tag chips (Genre/Setting/ContentWarning/CrossoverFandom) + character chips.</summary>
    public IReadOnlyList<TagChipDto> Tags { get; set; } = [];

    /// <summary>Per-character display entries with OC overlay data.</summary>
    public IReadOnlyList<CharacterDisplayEntry> Characters { get; set; } = [];

    /// <summary>Character pairings (ships) with resolved member names.</summary>
    public IReadOnlyList<PairingDisplayEntry> Pairings { get; set; } = [];

    /// <summary>
    /// "Also posted on" links (Feature 53 reframe, WU38d) — rendered low-key near the bottom of the
    /// story page (after chapters, before recommendations). Verified links show a checkmark.
    /// </summary>
    public IReadOnlyList<StoryExternalLinkDto> ExternalLinks { get; set; } = [];

    /// <summary>
    /// Legacy: chapter title list from the original L5 JSON-endpoint design. The story landing page
    /// (WU25) uses <see cref="IChapterReadService.GetChapterListAsync"/> instead for richer
    /// per-chapter metadata. This field will be removed when an L5 endpoint is rebuilt (post-MVP).
    /// </summary>
    public List<string> ChapterNames { get; set; } = [];
}

/// <summary>Per-character display data for the story view page (OC overlay info).</summary>
public sealed record CharacterDisplayEntry(
    TagChipDto Chip,
    TagPriority Priority,
    bool IsOc,
    string? OcName,
    string? OcBio);

/// <summary>Character pairing display row — member names already resolved from tag names.</summary>
public sealed record PairingDisplayEntry(
    CharacterPairingType PairingType,
    TagPriority Priority,
    IReadOnlyList<string> MemberNames)
{
    public string Separator => PairingType == CharacterPairingType.Romantic ? " ♥ " : " & ";
    public string DisplayNames => string.Join(Separator, MemberNames);
}