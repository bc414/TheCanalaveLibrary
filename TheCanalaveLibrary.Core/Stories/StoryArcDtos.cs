namespace TheCanalaveLibrary.Core;

/// <summary>
/// One author-defined chapter grouping of a story (WU45). Ordering is by
/// <paramref name="StartChapterNumber"/> — the ordinal "Arc X" label is the DTO's position in the
/// service-returned list (computed, never stored; the entity has no SortOrder).
/// </summary>
public record StoryArcDto(
    int StoryArcId,
    string Title,
    int StartChapterNumber,
    int EndChapterNumber);

/// <summary>Create payload for a new arc on a story the caller authors (WU45).</summary>
public record CreateStoryArcDto(
    int StoryId,
    string Title,
    int StartChapterNumber,
    int EndChapterNumber);

/// <summary>Update payload for an existing arc (WU45). All range/title rules re-validate.</summary>
public record UpdateStoryArcDto(
    int StoryArcId,
    string Title,
    int StartChapterNumber,
    int EndChapterNumber);
