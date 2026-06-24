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

    public const string RecommendationAccentColor = "#5BB85A";
    public const string RecommendationLabel = "Recommendations";

    // Faceted gem — kite/diamond silhouette with a CCW crown-facet subpath
    // that cancels winding inside to suggest the gem's upper face.
    // AccentColor: #1FA37A Torterra Emerald
    public const string HiddenGemIconPath =
        "M12 2L22 10L12 22L2 10Z " +
        "M12 4L12 10L20 10Z";

    public const string HiddenGemAccentColor = "#1FA37A";
    public const string HiddenGemLabel = "Hidden Gems";
}
