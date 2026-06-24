namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Recommendations service contract. Inherits the read interface so callers
/// that need both read and write inject only the narrowest applicable interface.
/// </summary>
public interface IRecommendationWriteService : IRecommendationReadService
{
    /// <summary>
    /// Submits a new recommendation. Sanitizes and validates the body (min
    /// <see cref="RecommendationConstants.MinLength"/> plain-text characters). Auto-approves on
    /// submit for MVP (moderation lifecycle deferred to WU34). One-per-user-per-story enforced by
    /// the DB unique index — duplicate submissions translate to a friendly validation error.
    /// </summary>
    /// <returns>The new <c>RecommendationId</c>.</returns>
    /// <exception cref="RecommendationValidationException">Body too short.</exception>
    /// <exception cref="KeyNotFoundException">Story not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated, or a recommendation already exists for this user+story.</exception>
    Task<int> SubmitAsync(RecommendationSubmitDto dto);

    /// <summary>
    /// Edits the body of an existing recommendation. Author-only. Re-sanitizes and re-validates.
    /// </summary>
    /// <exception cref="RecommendationValidationException">Body too short.</exception>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the recommendation's author.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task EditAsync(UpdateRecommendationDto dto);

    /// <summary>
    /// Hard-deletes a recommendation. Author-only. Cascade deletes all
    /// <c>RecommendationLike</c> and <c>RecommendationDetail</c> rows.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the recommendation's author.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task DeleteAsync(int recommendationId);

    /// <summary>
    /// Toggles a like on a recommendation. Returns the updated denormalized count and the
    /// caller's new like state. No notification — anti-addictive design (§6.11).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<RecommendationLikeResultDto> ToggleLikeAsync(int recommendationId);

    /// <summary>
    /// Sets or clears the Hidden Gem designation on a recommendation. Recommender-only.
    /// Setting to <c>true</c> rejects when the caller already has
    /// <see cref="RecommendationConstants.MaxHiddenGemsPerUser"/> active Hidden Gems (reject-at-limit,
    /// no auto-evict). On successful set, fires a best-effort
    /// <see cref="INotificationWriteService.NotifyStoryHiddenGemAsync"/> to the story author.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the recommendation's author.</exception>
    /// <exception cref="InvalidOperationException">Caller already holds the maximum Hidden Gems, or caller is not authenticated.</exception>
    Task SetHiddenGemAsync(int recommendationId, bool isHiddenGem);

    /// <summary>
    /// Sets or clears the story author's spotlight on a recommendation. Story-author-only.
    /// Setting to <c>true</c> rejects when the story already has
    /// <see cref="RecommendationConstants.MaxHighlightedPerStory"/> spotlighted recommendations.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    /// <exception cref="InvalidOperationException">Story already has the maximum spotlighted count, or caller is not authenticated.</exception>
    Task SetHighlightedByAuthorAsync(int recommendationId, bool isHighlighted);

    /// <summary>
    /// Records that a recommendation successfully led to a completed reading experience.
    /// Idempotent on composite PK. Increments <c>SuccessfulRecCount</c> on the recommendation.
    /// Minted in WU29; called by WU26 after Ch.1 IsRead triggers.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Recommendation not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task RecordSuccessAsync(int recommendationId);

    /// <summary>
    /// Records that the current user discovered a story via a specific recommendation (Feature 30
    /// attribution source). Written when the user opens a story from a recommendation link.
    /// Minted in WU29; called by WU26 reading-page infrastructure.
    /// </summary>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task RecordAttributionSourceAsync(int storyId, int recommendationId);
}
