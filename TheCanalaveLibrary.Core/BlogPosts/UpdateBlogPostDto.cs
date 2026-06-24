using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to update an existing blog post.
/// <see cref="Content"/> is raw HTML sanitized server-side before persisting.
/// The write service enforces author-only ownership (throws <see cref="UnauthorizedAccessException"/>
/// on mismatch between entity <c>AuthorId</c> and <see cref="IActiveUserContext.UserId"/>).
/// </summary>
public class UpdateBlogPostDto
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

    /// <summary>Optional FK to a story this post is about. Null = no linked story.</summary>
    public int? StoryId { get; set; }

    public bool IsPublished { get; set; }
}
