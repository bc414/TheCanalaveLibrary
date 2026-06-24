namespace TheCanalaveLibrary.Core;

/// <summary>
/// Result of a recommendation like toggle. <see cref="LikeCount"/> is the new denormalized count;
/// <see cref="IsLiked"/> is the caller's new like state. Mirrors <see cref="CommentLikeResultDto"/>.
/// No notification generated — anti-addictive design (§6.11).
/// </summary>
public record RecommendationLikeResultDto(int LikeCount, bool IsLiked);
