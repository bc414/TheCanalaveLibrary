namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to post a new chapter comment. <c>IsSpoiler</c> is chapter-specific (§5.9.1) —
/// it is not on the generic base DTO because profile/group/blog-post comments have no spoiler concept.
/// <c>CommentText</c> is raw HTML from <c>EditorView</c>; the write service sanitizes before
/// persisting (layer2-services.md §"User HTML Is Sanitized Once, On Save").
/// </summary>
public class PostChapterCommentDto
{
    public int ChapterId { get; set; }

    /// <summary>
    /// When set, this comment is a direct reply to an existing comment on the same chapter.
    /// The write service verifies the parent belongs to the same <c>ChapterId</c>.
    /// </summary>
    public long? ParentCommentId { get; set; }

    /// <summary>Raw HTML from EditorView — sanitized by the write service before persisting.</summary>
    public string CommentText { get; set; } = string.Empty;

    /// <summary>Marks comments containing spoilers for future chapters (§5.9.1).</summary>
    public bool IsSpoiler { get; set; }
}
