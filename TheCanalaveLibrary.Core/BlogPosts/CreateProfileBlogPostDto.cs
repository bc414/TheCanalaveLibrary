using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to create a <see cref="ProfileBlogPost"/>.
/// <see cref="Content"/> is raw HTML sanitized server-side before persisting (sanitize-once-on-save).
/// <c>AuthorId</c> is deliberately absent — the write service stamps it from
/// <see cref="IActiveUserContext.UserId"/> (mirrors <c>CreateStoryDTO</c>).
/// </summary>
public class CreateProfileBlogPostDto
{
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
