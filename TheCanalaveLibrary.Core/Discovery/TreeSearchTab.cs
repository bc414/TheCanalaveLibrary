namespace TheCanalaveLibrary.Core;

/// <summary>
/// The two tabs on the Unified Tree Search Page (spec §5.26). Ephemeral UI state — never carried
/// in the URL (only the root entity/anchor is, per §5.26 "URL state"). Mirrors <see cref="BookshelfTab"/>
/// / <see cref="ProfileTab"/> in convention (Core-level enum, not nested in a component).
/// </summary>
public enum TreeSearchTab
{
    /// <summary>The built tab (WU44) — the rCTE-backed flat, degree-annotated result list.</summary>
    Automatic = 0,

    /// <summary>Placeholder until Feature 33 / WU40 builds the graph visualization.</summary>
    Manual = 1,
}
