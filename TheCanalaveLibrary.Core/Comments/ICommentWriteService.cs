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
    /// Posts a new comment (or reply) on a group. Requires an authenticated user. Sanitizes
    /// <c>dto.CommentText</c> before persisting. If <c>dto.ParentCommentId</c> is set, verifies
    /// the parent comment belongs to the same group. No spoiler flag (group comments have no
    /// spoiler concept — only <see cref="ChapterComment.IsSpoiler"/> exists).
    /// </summary>
    /// <returns>The new <c>BaseComment.CommentId</c>.</returns>
    /// <exception cref="CommentValidationException">Thrown when text is empty.</exception>
    /// <exception cref="KeyNotFoundException">Group or parent comment not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<long> PostGroupCommentAsync(PostGroupCommentDto dto);

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
    /// Hard-deletes a comment. Authorized for the comment's author, OR — for chapter comments only
    /// (the one story-linked comment type) — the author of the story the comment sits on
    /// (WU-RecLifecycle author content control; see <c>content-safety.md</c> §"Author-Controlled
    /// Content Actions"). DB FKs handle the rest: <c>ParentCommentId</c> SET NULL reparents any
    /// replies as flat top-level comments; <c>CommentLike</c> rows CASCADE delete.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Comment not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is neither the comment's author nor the story's author.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task DeleteCommentAsync(long commentId);

    /// <summary>
    /// Posts a new comment (or reply) on a user profile wall. Requires an authenticated user.
    /// Sanitizes <c>dto.CommentText</c> before persisting. If <c>dto.ParentCommentId</c> is set,
    /// verifies the parent comment belongs to the same profile wall. The profile owner's
    /// <c>AllowProfileComments</c> setting is enforced <b>here</b> (WU-AccessGateSweep2 —
    /// <c>layer2-services.md</c> §"<c>AllowProfileComments</c> Gate"): the owner always passes;
    /// for anyone else <c>Public</c>/<c>UsersOnly</c> allow, <c>Following</c> requires the owner to
    /// follow the commenter, and <c>Nobody</c> (or an unknown value) refuses — root posts and
    /// replies alike. The check runs after the <c>ProfileVisibility</c> guard, so a hidden profile
    /// stays an indistinguishable not-found.
    /// No spoiler flag (profile-wall comments have no spoiler concept).
    /// </summary>
    /// <returns>The new <c>BaseComment.CommentId</c>.</returns>
    /// <exception cref="CommentValidationException">Text is empty, or the owner's
    /// <c>AllowProfileComments</c> setting refuses this commenter.</exception>
    /// <exception cref="KeyNotFoundException">Profile user not found or not visible to the caller, or parent comment not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<long> PostUserProfileCommentAsync(PostUserProfileCommentDto dto);

    /// <summary>
    /// Toggles a like on a comment. Requires an authenticated user. Returns the new
    /// <see cref="CommentLikeResultDto"/> with the updated denormalized <c>LikeCount</c> and the
    /// caller's new like state. No notification generated (§6.11 — anti-addictive design).
    /// A new like requires the comment and the content hosting it to be visible to the caller; an
    /// unlike (the caller already holds a like row) is a clear and always succeeds, even when the
    /// comment or its context is now hidden (owner ruling D6). The response is a read and stays
    /// gated: an unlike on a hidden comment returns <c>(LikeCount: 0, IsLiked: false)</c>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Comment not found, or (liking only) not visible to the caller.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<CommentLikeResultDto> ToggleLikeAsync(long commentId);
}
