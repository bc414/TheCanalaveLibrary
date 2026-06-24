namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to edit an existing comment's text. Author-only: the write service enforces
/// ownership (<c>comment.UserId == activeUser.UserId</c>) before applying the update.
/// <c>CommentText</c> is raw HTML; the write service re-sanitizes before persisting.
/// </summary>
public class UpdateCommentDto
{
    public long CommentId { get; set; }

    /// <summary>Raw HTML from EditorView — re-sanitized by the write service before persisting.</summary>
    public string CommentText { get; set; } = string.Empty;
}
