namespace TheCanalaveLibrary.Core;

/// <summary>
/// One ranked row on a /fanon axis page (WU-TagFanon Group 6): a cross-author custom-name group.
/// Reach counts are complete for every viewer (an OC name and a count are not mature content);
/// the expandable story list is what gets access-gated. <see cref="LinkedTargetTag"/> is non-null
/// once a moderator has pointed the group at an official tag.
/// </summary>
public sealed record FanonGroupDto(
    string Name,
    TagChipDto BaseTag,
    int StoryCount,
    int AuthorCount,
    TagChipDto? LinkedTargetTag,
    int NotifiedAuthorCount,
    /// <summary>Authors in the group with no adoption state yet — the mod "Notify new" count.</summary>
    int UnnotifiedAuthorCount);

/// <summary>A story row inside an expanded group — viewer-consent-filtered; the count-line
/// disclosure comes from <see cref="FanonGroupStoriesDto.TotalCount"/> vs the visible rows.</summary>
public sealed record FanonGroupStoryDto(int StoryId, string Title, int? AuthorId, string? AuthorName, Rating Rating);

/// <summary>Expanded story list for a group: visible rows plus the complete count (the
/// count-line disclosure states how many the viewer's consent hides).</summary>
public sealed record FanonGroupStoriesDto(IReadOnlyList<FanonGroupStoryDto> Visible, int TotalCount);

/// <summary>An established fanon tag for the /fanon hub.</summary>
public sealed record FanonTagDto(TagChipDto Tag, int StoryReach);

/// <summary>Moderator link request (WU-TagFanon Group 7): point a (name, base tag) group at an
/// existing target tag, or create the target inline first via ITagWriteService.</summary>
public sealed record FanonLinkCreateDto(string Name, int BaseTagId, int TargetTagId);

/// <summary>One affected row on the per-tag adoption page (Group 8).</summary>
public sealed record TagAdoptionRowDto(
    int StoryId,
    string StoryTitle,
    TagTypeEnum Axis,
    string BaseTagName,
    string CustomName,
    string? Nuance,
    /// <summary>The story already carries the target tag — adoption skips with an explanation
    /// rather than merging (merging would re-point pairing members).</summary>
    bool Collides);

/// <summary>The per-tag adoption page payload (notification target — /tag-adoptions/{tagId}).</summary>
public sealed record TagAdoptionPageDto(
    TagChipDto TargetTag,
    bool IsDismissed,
    IReadOnlyList<TagAdoptionRowDto> Rows);

/// <summary>One row on the standing /tag-adoptions index.</summary>
public sealed record MyTagAdoptionSummaryDto(
    TagChipDto TargetTag,
    int PendingRowCount,
    bool IsDismissed);

/// <summary>Adoption outcome: rows adopted vs rows skipped because the story already carries the
/// target tag.</summary>
public sealed record AdoptResultDto(int Adopted, int SkippedCollisions);
