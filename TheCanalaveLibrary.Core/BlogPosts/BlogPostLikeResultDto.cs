namespace TheCanalaveLibrary.Core;

/// <summary>
/// Result of toggling a blog post like. Mirrors <c>CommentLikeResultDto</c>.
/// <see cref="LikeCount"/> is the updated denormalized count; <see cref="IsLiked"/> reflects
/// the new state for the current user. Anti-addictive: no notification on like.
/// </summary>
public record BlogPostLikeResultDto(int LikeCount, bool IsLiked);
