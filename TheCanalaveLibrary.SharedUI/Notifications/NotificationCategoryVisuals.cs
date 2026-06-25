using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Maps each <see cref="NotificationCategoryEnum"/> to its icon, accent color, and display label.
/// Mirrors <see cref="BookshelfTabVisuals"/> (same dict + static accessor pattern).
///
/// <para>Icon paths that match an existing interaction/recommendation concept reuse the existing
/// constant as the single source of truth, exactly as <see cref="BookshelfTabVisuals"/> sources
/// from <see cref="UserStoryInteractionVisuals"/> and <see cref="RecommendationIcons"/>:</para>
/// <list type="bullet">
/// <item><term>YourFollows</term><description><see cref="UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum)"/> Follow (Manaphy Teal #2DBBA0)</description></item>
/// <item><term>YourRecommendations</term><description><see cref="RecommendationIcons.RecommendationIconPath"/> (Roserade Green #5BB85A)</description></item>
/// <item><term>Warnings</term><description><see cref="UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum)"/> Ignore (red #C04030)</description></item>
/// </list>
/// <para>New glyphs (24×24 viewBox, nonzero fill) are defined here for the five categories
/// with no existing equivalent: SiteNews, YourStories, YourProfile, Collaborations, Groups, YourReports.</para>
///
/// <para>L4 visual tokens (Tailwind classes on the rendering components) remain Stage 1 pending
/// visual sign-off — see <c>layer4-style.md</c> §"Notification icons (WU33)".</para>
/// </summary>
public static class NotificationCategoryVisuals
{
    public readonly record struct Info(string IconPath, string AccentColor, string Label);

    // ── New glyphs defined here (no existing equivalent in other visuals classes) ──

    /// <summary>Megaphone/loudspeaker — triangular horn + rectangular mouthpiece.</summary>
    private const string SiteNewsPath =
        "M2 10V14H7L14 18V6L7 10Z M16 8Q20 8 20 12Q20 16 16 16Z";

    /// <summary>Pencil over a page — writing/authorship. (Pen body reuses BookshelfTabVisuals MyStories path.)</summary>
    private const string YourStoriesPath =
        "M2 5H13V21H2Z M13 5H15V21H13Z M17 2L21 6L9 20L5 22L7 18Z";

    /// <summary>Person silhouette — head circle + shoulder curve.</summary>
    private const string YourProfilePath =
        "M12 4A4 4 0 0 1 16 8A4 4 0 0 1 12 12A4 4 0 0 1 8 8A4 4 0 0 1 12 4Z " +
        "M4 21C4 17 7.6 14 12 14C16.4 14 20 17 20 21Z";

    /// <summary>Chain links — two interlocked ovals with a horizontal connector.</summary>
    private const string CollaborationsPath =
        "M10 7H6A5 5 0 0 0 6 17H10M14 7H18A5 5 0 0 1 18 17H14M10 12H14";

    /// <summary>Two person silhouettes side by side.</summary>
    private const string GroupsPath =
        "M8 5A3 3 0 1 0 8 11A3 3 0 1 0 8 5Z " +
        "M2 20C2 17 4.7 15 8 15C11.3 15 14 17 14 20Z " +
        "M16 5A3 3 0 1 0 16 11A3 3 0 1 0 16 5Z " +
        "M22 20C22 17 19.3 15 16 15";

    /// <summary>Flag — vertical pole + pennant body.</summary>
    private const string YourReportsPath =
        "M3 3H5V21H3Z M5 3H19L14 9H19L14 16H5Z";

    private static readonly IReadOnlyDictionary<NotificationCategoryEnum, Info> Map =
        new Dictionary<NotificationCategoryEnum, Info>
        {
            [NotificationCategoryEnum.SiteNews] = new(
                SiteNewsPath, "#3B82F6", "Site News"),

            [NotificationCategoryEnum.YourFollows] = new(
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Follow).IconPath,
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Follow).AccentColor,
                "Your Follows"),

            [NotificationCategoryEnum.YourStories] = new(
                YourStoriesPath, "#2F7D4F", "Your Stories"),

            [NotificationCategoryEnum.YourProfile] = new(
                YourProfilePath, "#6366F1", "Your Profile"),

            [NotificationCategoryEnum.YourRecommendations] = new(
                RecommendationIcons.RecommendationIconPath,
                RecommendationIcons.RecommendationAccentColor,
                "Your Recommendations"),

            [NotificationCategoryEnum.Collaborations] = new(
                CollaborationsPath, "#0EA5E9", "Collaborations"),

            [NotificationCategoryEnum.Groups] = new(
                GroupsPath, "#7C3AED", "Groups"),

            [NotificationCategoryEnum.Warnings] = new(
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Ignore).IconPath,
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Ignore).AccentColor,
                "Warnings"),

            [NotificationCategoryEnum.YourReports] = new(
                YourReportsPath, "#F97316", "Your Reports"),
        };

    public static Info For(NotificationCategoryEnum category) => Map[category];

    /// <summary>All 9 categories in enum-value order (mirrors seed table ordering).</summary>
    public static IEnumerable<NotificationCategoryEnum> AllCategories =>
        Enum.GetValues<NotificationCategoryEnum>().OrderBy(c => (int)c);
}
