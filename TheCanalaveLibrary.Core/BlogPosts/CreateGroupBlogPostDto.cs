using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to create a new <see cref="GroupBlogPost"/>. Mirrors
/// <see cref="CreateProfileBlogPostDto"/> with the addition of <see cref="GroupId"/>.
/// <c>AuthorId</c> is server-stamped from <see cref="IActiveUserContext.UserId"/>; absent here.
/// <see cref="Content"/> is raw HTML sanitized server-side before persisting.
/// </summary>
public class CreateGroupBlogPostDto
{
    public int GroupId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Raw HTML from EditorView; sanitized server-side before persisting.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    public Rating Rating { get; set; }

    public bool HasSpoilers { get; set; }

    /// <summary>Optional FK to a story the post is about. SET NULL on story deletion.</summary>
    public int? StoryId { get; set; }
}

public static class CreateGroupBlogPostDtoValidations
{
    /// <summary>Returns validation errors, or an empty list when valid.</summary>
    public static List<string> CanSave(this CreateGroupBlogPostDto dto)
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
