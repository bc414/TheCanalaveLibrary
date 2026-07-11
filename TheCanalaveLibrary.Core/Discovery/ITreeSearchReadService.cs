namespace TheCanalaveLibrary.Core;

/// <summary>
/// Automatic Tree Search (Feature 59): live recursive-CTE traversal over the precomputed
/// <c>user_story_tree_search_entries</c> edge-list mart (rebuilt daily by the L8 worker —
/// `layer8-data-marts.md`). Rating (SFW/M) and the active viewer's interaction exclusions are
/// applied at the presentation join AFTER traversal — a mature story may act as a silent bridge
/// node without ever being shown. Traversal returns story IDs only and never reveals edge-owner
/// identity.
/// </summary>
public interface ITreeSearchReadService
{
    /// <summary>
    /// Runs one traversal. Throws <see cref="ArgumentException"/> when the request is malformed:
    /// no root / both roots, empty edge set, non-positive <c>MaxDegrees</c>, or
    /// <c>IncludePaths</c> with an edge set not ⊆ {HiddenGem, AuthorSpotlight}.
    /// </summary>
    Task<TreeSearchResultDto> TraverseAsync(TreeSearchRequest request, CancellationToken ct = default);

    /// <summary>
    /// The Automatic Tree Search UI composition (WU44): Source (this traversal) × Filter
    /// (<paramref name="filter"/>) × Sort (<see cref="TreeSearchRequest.Sort"/>) — see
    /// `layer2-services.md` "Tree Search — Automatic Tab Composition (WU44)" for the full design.
    ///
    /// <para>Runs the traversal with NO rating/interaction filter and NO cap (a "raw reached" mode —
    /// bounded by the same per-node fan-out guard as <see cref="TraverseAsync"/>), then hands the
    /// reached story ids to <see cref="IStoryReadService.FilterCandidateIdsAsync"/> — the same
    /// tag/FTS/interaction/rating predicate <c>/discover</c> uses — before sorting (Random or
    /// ByDegree), capping on the FILTERED set, and hydrating via
    /// <see cref="IStoryReadService.GetListingsByIdsAsync"/>. Tags/FTS/interaction exclusion never
    /// run inside the recursive CTE — see the audit note for why (silent-bridge correctness).</para>
    ///
    /// <para>Throws the same <see cref="ArgumentException"/>s as <see cref="TraverseAsync"/> for a
    /// malformed <paramref name="request"/>.</para>
    /// </summary>
    Task<TreeSearchListingResultDto> SearchAsync(
        TreeSearchRequest request, StoryFilterDto filter, CancellationToken ct = default);
}
