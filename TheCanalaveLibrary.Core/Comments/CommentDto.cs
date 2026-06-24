namespace TheCanalaveLibrary.Core;

/// <summary>
/// Display contract for a single comment across all contexts (chapter, profile, group, blog post).
/// Produced by the read service projection — the UI never sees <see cref="BaseComment"/> directly.
/// Author fields are nullable so WU20 can render the deleted-account ("[deleted user]") state.
/// <c>IsLikedByCurrentUser</c> is computed by the read service in its projection (per-viewer EXISTS
/// subquery on <see cref="CommentLike"/>); it then flows to the <c>CommentItem</c> leaf as a
/// <c>[Parameter]</c> — the leaf never injects a service.
/// </summary>
public record CommentDto(
    long CommentId,
    long? ParentCommentId,
    int? AuthorId,
    string? AuthorUsername,
    string? AuthorAvatarUrl,
    string CommentText,
    DateTime DatePosted,
    int LikeCount,
    bool IsSpoiler,
    bool IsLikedByCurrentUser);
