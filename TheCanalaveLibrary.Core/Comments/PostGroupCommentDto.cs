namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to post a new group comment (or reply). Mirrors
/// <see cref="PostBlogPostCommentDto"/> — no <c>IsSpoiler</c> flag (spoilers are a chapter-only
/// concept on <see cref="ChapterComment.IsSpoiler"/>).
/// <c>CommentText</c> is raw HTML from <c>EditorView</c>; sanitized server-side before persisting
/// (layer2-services.md §"User HTML Is Sanitized Once, On Save").
/// </summary>
public class PostGroupCommentDto
{
    public int GroupId { get; set; }

    /// <summary>
    /// When set, this comment is a direct reply to an existing comment on the same group.
    /// The write service verifies the parent belongs to the same <c>GroupId</c>.
    /// </summary>
    public long? ParentCommentId { get; set; }

    /// <summary>Raw HTML from EditorView — sanitized by the write service before persisting.</summary>
    public string CommentText { get; set; } = string.Empty;
}
