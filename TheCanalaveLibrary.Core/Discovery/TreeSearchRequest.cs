namespace TheCanalaveLibrary.Core;

/// <summary>
/// The two sanctioned result orderings for Automatic Tree Search (settled 2026-07-07):
/// two independent sort orders over the SAME result set — no path score, no edge weights.
/// </summary>
public enum TreeSearchSortOrder
{
    /// <summary>Random shuffle within the relevance envelope (the discoverability default).</summary>
    Random = 0,

    /// <summary>Minimum degree-to-reach ascending (closer connections first).</summary>
    ByDegree = 1,
}

/// <summary>
/// One Automatic Tree Search traversal request (Feature 59). Exactly one of
/// <see cref="RootStoryId"/> / <see cref="RootUserId"/> must be set (the spec's
/// /discover/story/{id} and /discover/user/{id} roots).
///
/// <para><see cref="IncludePaths"/> is valid ONLY when <see cref="EdgeTypes"/> ⊆
/// {HiddenGem, AuthorSpotlight} — the truly-capped chain-of-trust edges where a single path is
/// meaningful. Unbounded edges (Favorite, AuthoredBy, Recommendation, Vouch) yield
/// combinatorially noisy paths and never return one (`layer8-data-marts.md`).</para>
/// </summary>
public sealed record TreeSearchRequest
{
    public int? RootStoryId { get; init; }
    public int? RootUserId { get; init; }

    /// <summary>Degrees of connection to traverse (1 hop = 1 degree; story↔user alternation).
    /// ≈2 is "wide", 5–6 is "deep" (tractable only on the capped chain-of-trust edges).</summary>
    public required int MaxDegrees { get; init; }

    /// <summary>The edge types the searcher permits the traversal to follow (≥1).</summary>
    public required IReadOnlyList<TreeSearchEdgeType> EdgeTypes { get; init; }

    /// <summary>Materialize one shortest path per result story ("how you got here").
    /// Chain-of-trust edge sets only — see the type doc.</summary>
    public bool IncludePaths { get; init; }

    public TreeSearchSortOrder Sort { get; init; } = TreeSearchSortOrder.Random;

    /// <summary>Maximum result stories returned (post-filter). Guards supernode flooding.</summary>
    public int ResultCap { get; init; } = 100;
}

/// <summary>One story reached by the traversal, with its minimum degree-to-reach.</summary>
public sealed record TreeSearchHitDto
{
    public required int StoryId { get; init; }

    /// <summary>Minimum number of hops from the root at which this story was reached.</summary>
    public required int Degree { get; init; }

    /// <summary>
    /// One shortest traversal path (root → … → this story), only when
    /// <see cref="TreeSearchRequest.IncludePaths"/> was set on a chain-of-trust edge set.
    /// Raw node list (alternating user/story ids); rendering is a UI concern.
    /// </summary>
    public string? Path { get; init; }
}

/// <summary>Traversal outcome: the hits plus the envelope facts telemetry and the UI both need.</summary>
public sealed record TreeSearchResultDto
{
    public required IReadOnlyList<TreeSearchHitDto> Hits { get; init; }

    /// <summary>Deepest degree at which any returned story was first reached.</summary>
    public required int DegreesReached { get; init; }

    /// <summary>True when more post-filter hits existed than <see cref="TreeSearchRequest.ResultCap"/> —
    /// the flooding indicator (also emitted as the cap-truncation telemetry counter).</summary>
    public required bool ResultCapTruncated { get; init; }
}
