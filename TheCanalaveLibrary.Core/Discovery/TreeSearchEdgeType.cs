namespace TheCanalaveLibrary.Core;

/// <summary>
/// The six traversal edge types of the discovery graph (settled 2026-07-07, WU-Marts —
/// `layer8-data-marts.md` §"Edge taxonomy"). Every edge is a single (user, story) link worth
/// exactly 1 — there are NO edge-type weights; differentiation is provenance + fan-out cap.
/// Values are stored as <c>smallint</c> in the raw-SQL mart column
/// <c>user_story_tree_search_entries.edge_type</c> — never renumber.
/// </summary>
public enum TreeSearchEdgeType : short
{
    /// <summary>The user authored the story (<c>stories.author_id</c>). Wide tier.</summary>
    AuthoredBy = 0,

    /// <summary>
    /// The user favorited the story — public favorites, plus hidden favorites whose OWNER
    /// opted in via <c>allow_discovery_from_hidden_favorites</c> (materialized as a plain
    /// Favorite edge; no separate "boosted" type). Wide tier (unbounded fan-out).
    /// </summary>
    Favorite = 1,

    /// <summary>Approved, non-taken-down, non-anonymized recommendation. Mid tier.</summary>
    Recommendation = 2,

    /// <summary>
    /// Vouch projection: a vouch A→B (user→user, no story of its own) becomes one edge
    /// (A, s) per published story s authored by B. ≤5 vouchees per user but each with
    /// unbounded stories — mid tier, NOT chain-of-trust. Precomputed by the mart worker;
    /// never a live <c>vouches</c> join inside the rCTE.
    /// </summary>
    Vouch = 3,

    /// <summary>Hidden-gem recommendation (≤5 per user, self-conferred). Deep / chain-of-trust tier.</summary>
    HiddenGem = 4,

    /// <summary>
    /// Author-spotlighted recommendation ("hidden gem in reverse": the story's author confers
    /// the honor on ≤5 recommenders; lands on the (recommender, story) pair). Deep /
    /// chain-of-trust tier, first-class beside <see cref="HiddenGem"/>.
    /// </summary>
    AuthorSpotlight = 5,
}
