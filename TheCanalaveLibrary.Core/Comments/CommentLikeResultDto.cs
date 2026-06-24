namespace TheCanalaveLibrary.Core;

/// <summary>
/// Result of a like toggle, returned so the UI can reconcile optimistic state without a
/// separate read round-trip. <c>LikeCount</c> is the new denormalized count;
/// <c>IsLiked</c> is the caller's new like state (true = just liked, false = just unliked).
/// No <c>DateLiked</c> — anti-addictive design (§6.11).
/// </summary>
public record CommentLikeResultDto(int LikeCount, bool IsLiked);
