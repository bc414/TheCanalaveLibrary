namespace TheCanalaveLibrary.Core;

public interface IStoryReadService
{
    Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId);

    /// <summary>
    /// Gets the data required to edit a story.
    /// </summary>
    /// <param name="storyId">The ID of the story to edit.</param>
    /// <returns>A DTO containing the story's editable properties.</returns>
    Task<StoryUpdateDTO?> GetStoryForEditAsync(int storyId);

    /// <summary>
    /// Building-block method (spec §6.6) — turns story IDs that another service's domain query already
    /// selected (e.g. "which stories are favorited") into display-ready listing DTOs. That caller owns
    /// which IDs and what order; this owns the presentation projection. Results are reordered to match
    /// <paramref name="storyIds"/>; IDs filtered out by the content-rating master filter or otherwise
    /// not found are silently dropped, not erred.
    /// </summary>
    Task<StoryListingDto[]> GetListingsByIdsAsync(IReadOnlyList<int> storyIds);

    /// <summary>
    /// One simple unfiltered browse projection (most-recently-updated first), shaped for
    /// PaginationControls. Settled WU12: <c>GetListingsAsync(StoryFilterDto)</c> is deferred to WU23 —
    /// its filter shape isn't real until ResultsFilterPanel exists, and adding it later is purely
    /// additive (see audit/Stories.md Feature 5).
    /// </summary>
    Task<(StoryListingDto[] Items, int TotalCount)> GetRecentListingsAsync(int page, int pageSize);
}