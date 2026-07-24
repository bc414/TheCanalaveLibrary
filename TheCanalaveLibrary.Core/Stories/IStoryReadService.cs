namespace TheCanalaveLibrary.Core;

public interface IStoryReadService
{
    /// <summary>
    /// Reveal-aware since WU-AccessGate: an M story loads for viewers whose ceiling permits it,
    /// whose per-story reveal covers it, or for verified crawlers. Null still conflates
    /// absent/taken-down/gated — the null path calls <see cref="GetStoryGateAsync"/> to
    /// distinguish.
    /// </summary>
    Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId);

    /// <summary>
    /// The gated-existence read (WU-AccessGate): when a detail read returned null, distinguishes
    /// "exists but mature-gated" (returns interstitial metadata — title/author/rating only, no
    /// cover/description) from truly absent or taken down (returns null → real 404; the
    /// IsTakenDown filter stays ACTIVE here). Also serves chapter URLs — the chapter page gates
    /// on its parent story.
    /// </summary>
    Task<GatedMetadataDto?> GetStoryGateAsync(int storyId);

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
    /// global filter still applies; callers never see stories outside their rating cap — unless
    /// <paramref name="personalScope"/> is set (below).
    ///
    /// <b>Personal plane (<paramref name="personalScope"/>, WU-AccessGate):</b> set ONLY by
    /// Personal-plane surfaces (bookshelves, reading history, the owner's view of their own
    /// lists) whose candidate set is the viewer's own interaction graph — those reads are never
    /// rating-filtered (content-safety.md §"The Three-Plane Access Model": your own favorites/
    /// history show your M items regardless of your Discovery setting; this is what prevents
    /// invisible un-deletable ghost rows). Ignored unless <paramref name="restrictToStoryIds"/>
    /// is a non-empty set. Rides the HTTP query route for the WASM pass — a forged flag is a
    /// deliberate API call reading Class-B listing metadata of a self-supplied id set, which the
    /// Intentionality Doctrine deliberately does not defend against.
    /// </summary>
    Task<(StoryListingDto[] Items, int TotalCount)> GetListingsAsync(
        StoryFilterDto filter, IReadOnlyCollection<int>? restrictToStoryIds = null, bool personalScope = false);

    /// <summary>
    /// Mature count-line disclosure data (WU-AccessGate; person/collection-scoped listings only):
    /// interstitial-grade metadata (title/author/rating — no cover, no description) for the ids in
    /// <paramref name="storyIds"/> that the viewer's rating ceiling hid. Empty for mature-on
    /// viewers. Callers pass the surface's RAW candidate set (favorites ids, series entry ids,
    /// group story ids); global Discovery surfaces never call this.
    /// </summary>
    Task<IReadOnlyList<GatedMetadataDto>> GetGatedCardsAsync(IReadOnlyCollection<int> storyIds);

    /// <summary>
    /// Same disclosure data for an author's profile tab, where the visible-id read deliberately
    /// does NOT leak rating-hidden ids cross-user — this read supplies the hidden half as gated
    /// metadata instead (the discovery-bridge rule: acknowledge existence, withhold content).
    /// </summary>
    Task<IReadOnlyList<GatedMetadataDto>> GetGatedStoriesByAuthorAsync(int authorId);

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

    /// <summary>
    /// Incremental title-search typeahead (WU42) — <c>ILike</c> substring match, capped, ordered by
    /// title. Powers the reusable <c>StoryTitlePicker</c> component (Story Lineage's target picker,
    /// and the Groups add-story retrofit). Subject to the read context's
    /// <c>ContentRating</c>/<c>IsTakenDown</c> filters. <b>Deliberately not</b> the discovery FTS
    /// path (<see cref="GetListingsAsync"/>'s <c>TextQuery</c>) — <c>StoryListing.SearchVector</c> is
    /// a whole-word/ranked <c>to_tsvector</c> GIN index tuned for browse relevance, not incremental
    /// substring matching; the two access patterns don't share an index. Empty/whitespace
    /// <paramref name="term"/> returns an empty list (no "browse everything" fallback).
    /// </summary>
    Task<IReadOnlyList<StoryTitleSearchDto>> SearchStoriesByTitleAsync(string term);
}