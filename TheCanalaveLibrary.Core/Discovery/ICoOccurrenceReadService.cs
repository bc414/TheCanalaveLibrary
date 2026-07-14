namespace TheCanalaveLibrary.Core;

/// <summary>One related story from a co-occurrence mart, ranked by shared-user count.</summary>
public sealed record RelatedStoryScoreDto
{
    public required int RelatedStoryId { get; init; }

    /// <summary>Number of users who favorited (or recommended) both stories.</summary>
    public required int Score { get; init; }
}

/// <summary>
/// Also Favorited / Also Recommended (Feature 61): ranked reads of the story→story
/// co-occurrence result marts <c>also_favorited_scores</c> / <c>also_recommended_scores</c>
/// (raw-SQL, rebuilt daily — `layer8-data-marts.md`). The mart IS the cache (L7 dissolved
/// 2026-07-06): reads go straight to the indexed table; the viewer's rating filter and §8.7
/// interaction exclusions are applied at read time.
/// </summary>
public interface ICoOccurrenceReadService
{
    /// <summary>
    /// "Users who favorited this story also favorited…" — top <paramref name="take"/> by score.
    /// <paramref name="excludedInteractions"/> null (default) resolves the viewer's §8.7 defaults
    /// internally (unchanged prior behavior); non-null bypasses that lookup and is used as-is —
    /// lets a caller-driven filter (e.g. <c>UserStoryInteractionFilter</c>) override the default.
    /// </summary>
    Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoFavoritedAsync(
        int storyId, int take = 10,
        IReadOnlyList<UserStoryInteractionTypeEnum>? excludedInteractions = null,
        CancellationToken ct = default);

    /// <summary>
    /// "Users who recommended this story also recommended…" — top <paramref name="take"/> by score.
    /// See <see cref="GetAlsoFavoritedAsync"/> for the <paramref name="excludedInteractions"/> contract.
    /// </summary>
    Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoRecommendedAsync(
        int storyId, int take = 10,
        IReadOnlyList<UserStoryInteractionTypeEnum>? excludedInteractions = null,
        CancellationToken ct = default);
}
