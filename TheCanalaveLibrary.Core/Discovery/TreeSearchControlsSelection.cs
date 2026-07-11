namespace TheCanalaveLibrary.Core;

/// <summary>
/// Axis-emit contract (WU44) for <c>TreeSearchControls</c> — same naming discipline as
/// <c>TagFilterSelection</c> (`layer3.5-structure.md` "Filter-Axis Component Pattern"): a plain
/// descriptive name, not a <c>*Dto</c>, because it never crosses the service boundary on its own.
/// The hosting page (`TreeSearchDesktop`/`TreeSearchMobile`) merges this with the resolved root to
/// build the actual <see cref="TreeSearchRequest"/>.
/// </summary>
public sealed record TreeSearchControlsSelection
{
    public required int MaxDegrees { get; init; }
    public required IReadOnlyList<TreeSearchEdgeType> EdgeTypes { get; init; }
    public required TreeSearchSortOrder Sort { get; init; }

    /// <summary>
    /// Auto-derived by the control (never a raw checkbox) — true only when every selected edge is
    /// chain-of-trust ({HiddenGem, AuthorSpotlight}), the only combination the service accepts for
    /// path materialization.
    /// </summary>
    public required bool IncludePaths { get; init; }
}
