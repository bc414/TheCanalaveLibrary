namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Shared SVG icon constants for the Recommendations/Hidden Gems domain.
/// Owned by WU27 (bookshelves tab icons); consumed by WU29 (RecommendationCard).
/// All paths are 24×24 viewBox, single-color fill (nonzero rule).
///
/// IconPath values must not change without updating audit/UserStoryInteractions.md
/// Feature 17 bookshelf tab icon table first.
/// </summary>
public static class RecommendationIcons
{
    // Shooting star — 5-pointed star at top-right with two diagonal streak trails
    // from bottom-left, streaking bottom-left → top-right.
    // AccentColor: #5BB85A Roserade Green
    public const string RecommendationIconPath =
        "M17 3L19.2 5.4L22 7L19.2 8.6L17 11L14.8 8.6L12 7L14.8 5.4Z " +
        "M2 20L4 22L15 9L13 7Z " +
        "M5 22L7 22L17 11L15 9Z";

    public const string RecommendationAccentColor = "var(--color-rec)"; // value in app.css @theme (#5BB85A Roserade Green)
    public const string RecommendationLabel = "Recommendations";

    // Faceted gem — kite/diamond silhouette with a CCW crown-facet subpath
    // that cancels winding inside to suggest the gem's upper face.
    // AccentColor: #1FA37A Torterra Emerald
    public const string HiddenGemIconPath =
        "M12 2L22 10L12 22L2 10Z " +
        "M12 4L12 10L20 10Z";

    public const string HiddenGemAccentColor = "var(--color-gem)"; // value in app.css @theme (#1FA37A Torterra Emerald)
    public const string HiddenGemLabel = "Hidden Gems";

    /// <summary>
    /// Thumbs-up glyph for the helpful prompt's Yes control (spec §5.6, owner ruling D3 — "Yes
    /// (thumbs up)"). 24×24 viewBox, nonzero fill; inherits <c>currentColor</c> from its control.
    /// </summary>
    public const string HelpfulIconPath =
        "M1 21h4V9H1v12zm22-11c0-1.1-.9-2-2-2h-6.31l.95-4.57.03-.32c0-.41-.17-.79-.44-1.06L14.17 1 7.59 7.59C7.22 7.95 7 8.45 7 9v10c0 1.1.9 2 2 2h9c.83 0 1.54-.5 1.84-1.22l3.02-7.05c.09-.23.14-.47.14-.73v-2z";
}
