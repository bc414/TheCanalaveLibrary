namespace TheCanalaveLibrary.Core;

/// <summary>
/// One hydrated Automatic Tree Search result (WU44): a display-ready <see cref="StoryListingDto"/>
/// annotated with its minimum degree-to-reach and, for chain-of-trust edge sets, its raw traversal
/// path. Produced by <see cref="ITreeSearchReadService.SearchAsync"/> — see
/// `layer2-services.md` "Tree Search — Automatic Tab Composition (WU44)".
/// </summary>
public sealed record TreeSearchListingItemDto
{
    public required StoryListingDto Story { get; init; }

    /// <summary>Minimum number of hops from the root at which this story was reached.</summary>
    public required int Degree { get; init; }

    /// <summary>
    /// Raw Postgres CYCLE-clause path text (only present when the request's edge set was
    /// chain-of-trust and <c>IncludePaths</c> was set) — parse with <see cref="TreeSearchPathParser"/>
    /// before rendering. Never render a user-typed node's identity (privacy model, spec §5.4).
    /// </summary>
    public string? Path { get; init; }
}

/// <summary>
/// Outcome of <see cref="ITreeSearchReadService.SearchAsync"/>: the Source (traversal) × Filter
/// (<see cref="StoryFilterDto"/>) composition — see <see cref="TreeSearchResultDto"/> for the
/// unfiltered traversal-only counterpart used by <see cref="ITreeSearchReadService.TraverseAsync"/>.
/// </summary>
public sealed record TreeSearchListingResultDto
{
    public required IReadOnlyList<TreeSearchListingItemDto> Items { get; init; }

    /// <summary>Deepest degree at which any returned story was first reached.</summary>
    public required int DegreesReached { get; init; }

    /// <summary>True when more post-filter hits existed than <see cref="TreeSearchRequest.ResultCap"/> —
    /// computed against the FILTERED candidate count, not the raw traversal count (a filter can shrink
    /// the candidate set below the cap even when the raw traversal itself was truncated by fan-out).</summary>
    public required bool ResultCapTruncated { get; init; }
}
