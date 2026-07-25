namespace TheCanalaveLibrary.Core;

/// <summary>
/// Tree-node DTO for a <see cref="GroupFolder"/>. Used for both the display folder tree and the
/// folder-management panel. <see cref="Children"/> carries sub-folders (empty list = leaf node).
/// <see cref="Stories"/> carries the stories assigned to this folder, each with its
/// <see cref="GroupStoryDto.GroupStoryId"/> (needed by <c>UnassignStoryFromFolderAsync</c>) — not
/// bare <c>StoryId</c>s, so the folder-tree display and per-folder unassign controls both read from
/// this one collection instead of a parallel lookup.
/// </summary>
public record GroupFolderDto(
    int GroupFolderId,
    int GroupId,
    int? ParentFolderId,
    string Name,
    Rating MaxRating,
    int SortOrder,
    IReadOnlyList<GroupStoryDto> Stories,
    IReadOnlyList<GroupFolderDto> Children);
