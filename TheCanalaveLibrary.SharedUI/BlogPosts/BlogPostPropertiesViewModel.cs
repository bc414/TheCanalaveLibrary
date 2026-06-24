using System.ComponentModel.DataAnnotations;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// View model for <see cref="BlogPostPropertiesForm"/>. Carries UI-only state (<c>IsLoading</c>,
/// <c>ServerValidationErrors</c>) and shields the form from write-service DTOs' server-only fields
/// (<c>AuthorId</c>). The page owns the ViewModel↔DTO mapping and the EditorView pull-on-submit
/// call, keeping the form bUnit-testable with no dependencies. Mirrors
/// <see cref="StoryPropertiesViewModel"/>.
/// </summary>
public class BlogPostPropertiesViewModel
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(256, ErrorMessage = "Title cannot exceed 256 characters.")]
    public string Title { get; set; } = string.Empty;

    public Rating Rating { get; set; }

    public bool HasSpoilers { get; set; }

    /// <summary>Optional link to one of the author's stories (displayed on the blog-post view page).</summary>
    public int? StoryId { get; set; }

    /// <summary>
    /// Tracks the draft/publish toggle. New posts default to draft (<c>false</c>); the author
    /// explicitly publishes via the form toggle. The write service sets <c>IsPublished</c> directly
    /// from <see cref="UpdateBlogPostDto.IsPublished"/>; on create it is always <c>false</c>
    /// (server default).
    /// </summary>
    public bool IsPublished { get; set; }

    /// <summary>
    /// Populated from <c>EditorView.GetHtmlAsync()</c> by the page before mapping to the DTO.
    /// Not bound via two-way binding — EditorView uses pull-on-submit (layer3-logic.md
    /// §"EditorView Pull-on-Submit").
    /// </summary>
    public string? Content { get; set; }

    public bool IsLoading { get; set; }
    public List<string> ServerValidationErrors { get; set; } = new();
}
