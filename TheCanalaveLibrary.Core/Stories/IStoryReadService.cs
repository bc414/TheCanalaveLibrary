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
    ///
    /// <b>Bookshelf narrowing:</b> when <paramref name="restrictToStoryIds"/> is provided, results
    /// are pre-filtered to that candidate set before all other filters are applied. The content-rating
    /// global filter still applies; callers never see stories outside their rating cap.
    /// </summary>
    Task<(StoryListingDto[] Items, int TotalCount)> GetListingsAsync(
        StoryFilterDto filter, IReadOnlyCollection<int>? restrictToStoryIds = null);

    /// <summary>
    /// Returns a random selection of listing DTOs from the post-filter valid set (WU28, spec §5.3).
    /// Applies the same tag / FTS / interaction-exclusion filters as <see cref="GetListingsAsync"/>
    /// via the shared <c>ApplyFilters</c> helper, then orders by <c>EF.Functions.Random()</c> and
    /// takes up to <paramref name="batchSize"/> results.
    ///
    /// <b>No shown-id tracking.</b> "Give me more" is a second call that appends a fresh draw — the
    /// caller accumulates results for display; this method has no memory of prior calls. Repeats are
    /// acceptable (and expected). <see cref="StoryFilterDto.Sort"/> is ignored; sorting is always
    /// random. <see cref="StoryFilterDto.Page"/> / <see cref="StoryFilterDto.PageSize"/> are also
    /// ignored; <paramref name="batchSize"/> controls the take.
    /// </summary>
    Task<StoryListingDto[]> GetRandomBatchAsync(StoryFilterDto filter, int batchSize);

    /// <summary>
    /// Filters an arbitrary candidate id set down to the ones surviving <paramref name="filter"/>'s
    /// tag / FTS / interaction-exclusion predicate — the same <c>ApplyFilters</c> helper
    /// <see cref="GetListingsAsync"/> and <see cref="GetRandomBatchAsync"/> use — plus the always-on
    /// content-rating global filter. No sort, no pagination, no hydration: this is the thin
    /// building-block Automatic Tree Search composes against (WU44,
    /// `layer2-services.md` "Tree Search — Automatic Tab Composition"), for a Source whose candidate
    /// set comes from somewhere other than a plain <c>Stories</c> query (a graph traversal). Order of
    /// the returned ids is unspecified — callers that need order project it themselves.
    /// </summary>
    Task<IReadOnlyList<int>> FilterCandidateIdsAsync(IReadOnlyCollection<int> candidateIds, StoryFilterDto filter);

    /// <summary>
    /// Returns the IDs of all stories authored by <paramref name="authorId"/>, bypassing the
    /// content-rating filter so authors always see their own mature stories. Used by the
    /// My Stories bookshelf tab.
    /// </summary>
    Task<IReadOnlyList<int>> GetStoryIdsByAuthorAsync(int authorId);

    /// <summary>
    /// The seeded external-platform lookup (Feature 53 reframe, WU38d) — feeds the story form's
    /// "Also posted on" dropdown and its paste-a-URL platform auto-detection (DomainPattern).
    /// </summary>
    Task<IReadOnlyList<ExternalPlatformDto>> GetExternalPlatformsAsync();

    /// <summary>
    /// Lifetime total views of a story — <c>SUM</c> over its <c>daily_story_stats</c> rows
    /// (Feature 45; the per-story/day accumulation the view buffer flushes into). Fetched
    /// on-demand only (the story-card dropdown reveal) — deliberately not part of any listing
    /// projection, and <b>never a sort key</b> (non-sortable informational metric by design).
    /// Approximate by contract: buffered views land within the flush interval.
    /// </summary>
    Task<long> GetStoryTotalViewsAsync(int storyId);
}