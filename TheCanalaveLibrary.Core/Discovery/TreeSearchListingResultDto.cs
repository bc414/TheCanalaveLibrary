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
    /// chain-of-trust and <c>IncludePaths</c> was set) — parse with <see cref="TreeSearchPathParser"/>.
    /// Prefer <see cref="PathHops"/> for display; this raw form remains for consumers that only
    /// need ids.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Display-hydrated path hops (WU40 privacy correction, 2026-07-12): chain-of-trust paths
    /// carry no anonymized contributor — every hop is a public curated act — so BOTH story and
    /// user hops render with real, linkable identity. A hop whose <c>Label</c> is null is one
    /// the viewer cannot see (e.g. a rating-gated bridge story) and renders as an opaque
    /// <c>#id</c>. Null when the request carried no paths.
    /// </summary>
    public IReadOnlyList<TreeSearchPathHopDto>? PathHops { get; init; }
}

/// <summary>One display-ready hop of a chain-of-trust path: story (title) or user (username).</summary>
public sealed record TreeSearchPathHopDto(bool IsStory, int Id, string? Label);

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
