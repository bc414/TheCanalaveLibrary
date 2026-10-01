using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to update an existing <see cref="GroupBlogPost"/> (WU-TptHardDelete, owner ruling
/// D10's group-post lifecycle). Separate from <see cref="UpdateBlogPostDto"/>, which addresses
/// profile posts only (see <see cref="IBlogPostWriteService.UpdateBlogPostAsync"/>'s doc), rather than
/// overloading it with a type switch. No <c>IsPublished</c>: group posts publish on create. No
/// <c>StoryId</c>: group posts are not story-linkable (WU-B2). The write service enforces author-only
/// ownership; it does not recheck group membership (the author owns their row and may have left).
/// </summary>
public class UpdateGroupBlogPostDto
{
    public int BlogPostId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Raw HTML from EditorView; sanitized server-side before persisting.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    public Rating Rating { get; set; }

    public bool HasSpoilers { get; set; }
}

public static class UpdateGroupBlogPostDtoValidations
{
    /// <summary>Returns validation errors, or an empty list when valid.</summary>
    public static List<string> CanSave(this UpdateGroupBlogPostDto dto)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Title))
            errors.Add("Title must not be empty.");
        else if (dto.Title.Length > 256)
            errors.Add("Title must be 256 characters or fewer.");
        if (string.IsNullOrWhiteSpace(dto.Content))
            errors.Add("Content must not be empty.");
        return errors;
    }
}
