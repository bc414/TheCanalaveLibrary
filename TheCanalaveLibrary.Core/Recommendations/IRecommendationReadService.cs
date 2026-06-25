namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Recommendations service contract (Features 27–30).
/// No-tracking projections; per-viewer fields (<c>IsLikedByCurrentUser</c>,
/// <c>IsOwnRecommendation</c>) computed via filtered Include on the read side.
/// </summary>
public interface IRecommendationReadService
{
    /// <summary>
    /// Returns all Approved recommendations for a story, ordered: spotlighted
    /// (<c>IsHighlightedByAuthor</c>) first, then by <c>DatePosted</c> descending.
    /// Per-viewer <c>IsLikedByCurrentUser</c> and <c>IsOwnRecommendation</c> are included.
    /// </summary>
    Task<List<RecommendationDto>> GetForStoryAsync(int storyId);

    /// <summary>Returns a single recommendation by id, or null if not found or not Approved.</summary>
    Task<RecommendationDto?> GetByIdAsync(int recommendationId);

    /// <summary>
    /// Returns the IDs of stories the active user has written an approved recommendation for.
    /// Anonymous → empty. Used by the Recommendations bookshelf tab ("My Recommendations").
    /// </summary>
    Task<IReadOnlyList<int>> GetRecommendedStoryIdsAsync();

    /// <summary>
    /// Returns the IDs of stories the active user has written an approved Hidden Gem
    /// recommendation for. Anonymous → empty. Used by the Hidden Gems bookshelf tab.
    /// </summary>
    Task<IReadOnlyList<int>> GetHiddenGemStoryIdsAsync();

    /// <summary>
    /// Returns the <c>RecommendationId</c> from the viewer's
    /// <see cref="UserStoryRecommendationSource"/> for <paramref name="storyId"/> — but only when
    /// no <see cref="RecommendationSuccess"/> already exists for (viewer, that recommendation).
    /// Used by the reading page to gate the "found this helpful?" prompt (WU26, spec §5.6).
    /// Anonymous → null. No source row → null. Success already recorded → null.
    /// </summary>
    Task<int?> GetHelpfulPromptRecommendationIdAsync(int storyId);

    /// <summary>
    /// Returns the IDs of stories that <paramref name="userId"/> has written an Approved
    /// recommendation for. Used by the profile page's Recommendations tab as the candidate ID
    /// set, passed to <see cref="IStoryReadService.GetListingsAsync"/> with
    /// <c>restrictToStoryIds</c>. Public — any viewer may see another user's recommendations.
    /// </summary>
    Task<IReadOnlyList<int>> GetRecommendedStoryIdsByUserAsync(int userId);
}
