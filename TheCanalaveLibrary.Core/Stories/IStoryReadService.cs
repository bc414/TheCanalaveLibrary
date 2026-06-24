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
    /// PaginationControls. Kept for home-page hot-path (no filter overhead); use
    /// <see cref="GetListingsAsync"/> when the caller has filter criteria.
    /// </summary>
    Task<(StoryListingDto[] Items, int TotalCount)> GetRecentListingsAsync(int page, int pageSize);

    /// <summary>
    /// Full filtered listing query (WU23, spec §5.27). Source=All (every story visible to the
    /// current viewer, modulo the global content-rating filter). Two-step: filtered IQueryable →
    /// scalar id page → <see cref="GetListingsByIdsAsync"/> for the presentation projection.
    ///
    /// <b>Sort rules:</b> <see cref="DefaultSortOrder.Relevance"/> falls back to
    /// <see cref="DefaultSortOrder.DatePublished"/> when <paramref name="filter"/>.TextQuery is
    /// null/empty. <see cref="DefaultSortOrder.Score"/> is not meaningful on Source=All and
    /// falls back to DatePublished as well.
    ///
    /// <b>Interaction exclusions:</b> applied only when the viewer is authenticated
    /// (<see cref="IActiveUserContext.UserId"/> is non-null); anonymous viewers see everything.
    /// </summary>
    Task<(StoryListingDto[] Items, int TotalCount)> GetListingsAsync(StoryFilterDto filter);
}