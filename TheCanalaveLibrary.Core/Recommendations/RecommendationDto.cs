namespace TheCanalaveLibrary.Core;

/// <summary>
/// Display contract for a single recommendation. Produced by the read service projection — the
/// UI never sees the <see cref="Recommendation"/> entity directly.
/// <para><see cref="Recommender"/> uses the attribution variant of <see cref="UserCardDto"/>
/// (§5.30.7 #2 — same card shape, rendered in recommendation context). Nullable to support the
/// anonymized-account ("[deleted user]") state (same pattern as <see cref="CommentDto"/>).</para>
/// <para><see cref="IsLikedByCurrentUser"/> is computed by the read service via a per-viewer
/// filtered Include; <see cref="IsOwnRecommendation"/> gates edit/delete/Hidden-Gem affordances
/// without a separate RPC.</para>
/// <para><b>WU-RecLifecycle additive fields</b>: <see cref="Status"/> +
/// <see cref="RevisionRequestNote"/>. Public reads always project <c>Approved</c>/<c>null</c>
/// (they only return Approved recs); non-Approved status and the author's note are populated
/// ONLY in the author-view and own-rec projections — never leaked to public viewers.</para>
/// </summary>
public record RecommendationDto(
    int RecommendationId,
    int StoryId,
    UserCardDto? Recommender,
    string BodyHtml,
    int LikeCount,
    bool IsHiddenGem,
    bool IsHighlightedByAuthor,
    int SuccessfulRecCount,
    DateTime DatePosted,
    bool IsLikedByCurrentUser,
    bool IsOwnRecommendation,
    RecommendationStatusEnum Status = RecommendationStatusEnum.Approved,
    string? RevisionRequestNote = null);
