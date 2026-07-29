using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to update an existing <see cref="SiteBlogPost"/>. Separate from
/// <see cref="UpdateBlogPostDto"/> (which only ever touches <c>profile_blog_posts</c> child
/// columns — see <see cref="IBlogPostWriteService.UpdateBlogPostAsync"/>'s doc) rather than
/// overloading it with a type switch. The write service enforces <c>IsModerator || IsAdmin</c>
/// (any moderator/admin manages any site post — the <see cref="SitePoll"/> precedent, not the
/// author-only rule the other blog post types use).
/// </summary>
public class UpdateSiteBlogPostDto
{
    public int BlogPostId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    /// <summary>Raw HTML from EditorView; sanitized server-side before persisting.</summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    public bool IsPublished { get; set; }

    /// <summary>Fan the SiteAnnouncement notification out to every user once this post publishes.
    /// Editable only until <see cref="SiteBlogPost.NotifiedAtUtc"/> is set — see the write
    /// service's fire-once guard.</summary>
    public bool NotifyAllUsers { get; set; }
}

public static class UpdateSiteBlogPostDtoValidations
{
    /// <summary>Returns validation errors, or an empty list when valid.</summary>
    public static List<string> CanSave(this UpdateSiteBlogPostDto dto)
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
