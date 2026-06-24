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
}
