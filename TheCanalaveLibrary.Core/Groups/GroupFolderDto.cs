namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tree-node DTO for a <see cref="GroupFolder"/>. Used for both the display folder tree and the
/// folder-management panel. <see cref="Children"/> carries sub-folders (empty list = leaf node).
/// <see cref="StoryIds"/> carries the <see cref="GroupStory.StoryId"/> values assigned to this folder.
/// </summary>
public record GroupFolderDto(
    int GroupFolderId,
    int GroupId,
    int? ParentFolderId,
    string Name,
    Rating MaxRating,
    int SortOrder,
    IReadOnlyList<int> StoryIds,
    IReadOnlyList<GroupFolderDto> Children);
