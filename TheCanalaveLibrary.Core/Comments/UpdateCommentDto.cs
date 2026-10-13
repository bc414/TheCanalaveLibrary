namespace TheCanalaveLibrary.Core;

/// <summary>
/// Data required to edit an existing comment's text (and, on a chapter comment, its spoiler flag).
/// Author-only: the write service enforces ownership (<c>comment.UserId == activeUser.UserId</c>)
/// before applying the update. <c>CommentText</c> is raw HTML; the write service re-sanitizes before
/// persisting.
/// </summary>
public class UpdateCommentDto
{
    public long CommentId { get; set; }

    /// <summary>Raw HTML from EditorView — re-sanitized by the write service before persisting.</summary>
    public string CommentText { get; set; } = string.Empty;

    /// <summary>
    /// The new spoiler flag, or null to leave it unchanged (WU-ParityAndRemaining, 2026-10-13).
    /// Spoilers are chapter-only, so a non-null value on any other context is a
    /// <c>CommentValidationException</c>; <c>CommentSection</c> sends it only for chapter-context roots.
    /// </summary>
    public bool? IsSpoiler { get; set; }
}
