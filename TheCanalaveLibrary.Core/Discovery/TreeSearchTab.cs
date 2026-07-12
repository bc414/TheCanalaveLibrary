namespace TheCanalaveLibrary.Core;

/// <summary>
/// The three tabs on the Unified Tree Search Page — Automatic (WU44) plus manual tree search's
/// two paradigms, Explore and Deep Dive (Feature 33 / WU40; the WU44-era single "Manual"
/// placeholder split into these two — a deliberate, recorded divergence from spec §5.26's
/// literal "two tabs"; see `audit/Discovery.md` F33). Ephemeral UI state — never carried in the
/// URL (only the root entity/anchor is, per §5.26 "URL state"). Mirrors <see cref="BookshelfTab"/>
/// / <see cref="ProfileTab"/> in convention (Core-level enum, not nested in a component).
/// </summary>
public enum TreeSearchTab
{
    /// <summary>WU44 — the rCTE-backed flat, degree-annotated result list.</summary>
    Automatic = 0,

    /// <summary>WU40 — build-your-own-map: curated tree + stateless candidate-results pane.</summary>
    Explore = 1,

    /// <summary>WU40 — bounded chain-of-trust walking: full-viewport tree, click auto-adds.</summary>
    DeepDive = 2,
}
