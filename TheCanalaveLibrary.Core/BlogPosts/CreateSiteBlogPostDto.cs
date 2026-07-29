using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to create a new <see cref="SiteBlogPost"/>. Mirrors <see cref="CreateGroupBlogPostDto"/>
/// with the removal of <c>Rating</c>/<c>HasSpoilers</c> (not meaningful for staff announcements)
/// and the addition of <see cref="NotifyAllUsers"/>. <c>AuthorId</c> is server-stamped from
/// <see cref="IActiveUserContext.UserId"/>; absent here.
/// </summary>
public class CreateSiteBlogPostDto
{
    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Raw HTML from EditorView; sanitized server-side before persisting.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    /// <summary>Fan the SiteAnnouncement notification out to every user once this post publishes.</summary>
    public bool NotifyAllUsers { get; set; }
}

public static class CreateSiteBlogPostDtoValidations
{
    /// <summary>Returns validation errors, or an empty list when valid.</summary>
    public static List<string> CanSave(this CreateSiteBlogPostDto dto)
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
