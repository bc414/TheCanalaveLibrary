namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Comments service contract. Inherits the read interface so callers that need
/// both read and write inject only the narrowest applicable interface (layer2-services.md
/// §"CQRS-Lite with Inheritance"). Edit and delete are author-only; moderation delete is WU34.
/// </summary>
public interface ICommentWriteService : ICommentReadService
{
    /// <summary>
    /// Posts a new comment (or reply) on a chapter. Requires an authenticated user. Sanitizes
    /// <c>dto.CommentText</c> before persisting. If <c>dto.ParentCommentId</c> is set, verifies
    /// the parent comment belongs to the same chapter.
    /// </summary>
    /// <returns>The new <c>BaseComment.CommentId</c>.</returns>
    /// <exception cref="CommentValidationException">Thrown when text is empty.</exception>
    /// <exception cref="KeyNotFoundException">Chapter or parent comment not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<long> PostChapterCommentAsync(PostChapterCommentDto dto);

    /// <summary>
    /// Posts a new comment (or reply) on a blog post. Requires an authenticated user. Sanitizes
    /// <c>dto.CommentText</c> before persisting. If <c>dto.ParentCommentId</c> is set, verifies
    /// the parent comment belongs to the same blog post. No spoiler flag (spoiler lives on the
    /// post itself via <see cref="ProfileBlogPost.HasSpoilers"/>).
    /// </summary>
    /// <returns>The new <c>BaseComment.CommentId</c>.</returns>
    /// <exception cref="CommentValidationException">Thrown when text is empty.</exception>
    /// <exception cref="KeyNotFoundException">Blog post or parent comment not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<long> PostBlogPostCommentAsync(PostBlogPostCommentDto dto);

    /// <summary>
    /// Edits the text of an existing comment. Author-only: throws
    /// <see cref="UnauthorizedAccessException"/> if the caller is not the comment's author.
    /// Re-sanitizes the new text before persisting.
    /// </summary>
    /// <exception cref="CommentValidationException">Thrown when new text is empty.</exception>
    /// <exception cref="KeyNotFoundException">Comment not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the comment's author.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task EditCommentAsync(UpdateCommentDto dto);

    /// <summary>
    /// Hard-deletes a comment. Author-only. DB FKs handle the rest: <c>ParentCommentId</c> SET NULL
    /// reparents any replies as flat top-level comments; <c>CommentLike</c> rows CASCADE delete.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Comment not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the comment's author.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task DeleteCommentAsync(long commentId);

    /// <summary>
    /// Toggles a like on a comment. Requires an authenticated user. Returns the new
    /// <see cref="CommentLikeResultDto"/> with the updated denormalized <c>LikeCount</c> and the
    /// caller's new like state. No notification generated (§6.11 — anti-addictive design).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Comment not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<CommentLikeResultDto> ToggleLikeAsync(long commentId);
}
