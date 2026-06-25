using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to create a new <see cref="GroupFolder"/>. Admin-only.
/// <see cref="MaxRating"/> must be ≤ the parent group's <see cref="Group.MaxContentRating"/>
/// (enforced in the write service; throws <see cref="GroupValidationException"/> on violation).
/// </summary>
public class CreateFolderDto
{
    public int GroupId { get; set; }

    public int? ParentFolderId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Content-rating ceiling for this folder. Must be ≤ the parent group's
    /// <see cref="Group.MaxContentRating"/>. Defaults to <see cref="Rating.M"/> (no restriction
    /// beyond the group cap).
    /// </summary>
    public Rating MaxRating { get; set; } = Rating.M;

    public int SortOrder { get; set; }
}
