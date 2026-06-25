namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to add a story to a group (member action, subject to the content-rating waterfall).
/// <see cref="GroupFolderId"/> is optional: when set, the story is also assigned to that folder
/// (the write service validates folder membership in the same group and the folder-level MaxRating).
/// </summary>
public class AddGroupStoryDto
{
    public int GroupId { get; set; }
    public int StoryId { get; set; }

    /// <summary>
    /// Optional folder to assign the story to in addition to the group-level add.
    /// When <c>null</c>, the story is added at the group level with no folder assignment.
    /// </summary>
    public int? GroupFolderId { get; set; }
}
