namespace TheCanalaveLibrary.Core;

/// <summary>
/// The (edge, direction) pairs of Manual Tree Search (Feature 33 / WU40). Every pair is a
/// DISTINCT traversal semantic and is independently toggleable in the UI — "recommendation from
/// a story" (who recommended it) and "recommendation from a user" (what they recommended) are
/// different questions and never share one flag; conflating them produced the Author×Pinned
/// identity-round-trip bug the mock surfaced (see `audit/Discovery.md` Feature 33).
///
/// <para>Deliberately independent of the frozen mart enum <see cref="TreeSearchEdgeType"/>
/// ("never renumber" — raw-SQL smallint contract). This enum is UI/tree-state-only: it labels
/// tree-node connectors, colors edge lines, and serializes into the client-side localStorage
/// tree document. It never touches the mart or the database.</para>
///
/// <para>Deep Dive's whitelist is the four pairs whose immediate result is bounded to ≤1 or ≤5:
/// <see cref="StoryAuthor"/> (1), <see cref="UserGemmedStory"/> (≤5),
/// <see cref="StorySpotlightedRecommender"/> (≤5), <see cref="UserPinnedStory"/> (1).</para>
/// </summary>
public enum ManualTreeEdge
{
    // ── Story anchor → user targets ─────────────────────────────────────────────
    /// <summary>story → its author (exactly 1; identity connector).</summary>
    StoryAuthor = 0,
    /// <summary>story → a user who recommended it (recommendation family; unbounded).</summary>
    StoryRecommender = 1,
    /// <summary>story → one of its ≤5 author-spotlighted recommenders (bounded; Deep Dive).</summary>
    StorySpotlightedRecommender = 2,
    /// <summary>story → a user who publicly favorited it (unbounded).</summary>
    StoryFavoriter = 3,

    // ── User anchor → story targets ─────────────────────────────────────────────
    /// <summary>user → a story they recommended (recommendation family; unbounded).</summary>
    UserRecommendedStory = 4,
    /// <summary>user → one of their own ≤5 hidden-gem stories (bounded; Deep Dive).</summary>
    UserGemmedStory = 5,
    /// <summary>user → a story they publicly favorited (unbounded).</summary>
    UserFavoriteStory = 6,
    /// <summary>user → a story they authored (their full catalog; unbounded).</summary>
    UserAuthoredStory = 7,
    /// <summary>user → their single pinned story (exactly ≤1; Deep Dive — the AuthoredBy mirror
    /// that lets the Author Spotlight chain self-sustain).</summary>
    UserPinnedStory = 8,
    /// <summary>user → a published story authored by one of their ≤5 vouchees (the projected
    /// story set is unbounded — Explore only, never Deep Dive).</summary>
    UserVouchedStory = 9,
}
