namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to post a new blog-post comment (or reply).
/// No <c>IsSpoiler</c> flag — spoiler information lives on the post itself
/// (<see cref="ProfileBlogPost.HasSpoilers"/>), not on individual comments.
/// <c>CommentText</c> is raw HTML from <c>EditorView</c>; the write service sanitizes before
/// persisting (layer2-services.md §"User HTML Is Sanitized Once, On Save").
/// </summary>
public class PostBlogPostCommentDto
{
    public int BlogPostId { get; set; }

    /// <summary>
    /// When set, this comment is a direct reply to an existing comment on the same blog post.
    /// The write service verifies the parent belongs to the same <c>BlogPostId</c>.
    /// </summary>
    public long? ParentCommentId { get; set; }

    /// <summary>Raw HTML from EditorView — sanitized by the write service before persisting.</summary>
    public string CommentText { get; set; } = string.Empty;
}
