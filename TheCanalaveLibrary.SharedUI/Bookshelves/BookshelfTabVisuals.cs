using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Maps each <see cref="BookshelfTab"/> to its icon, accent color, display label, and URL slug.
/// The six interaction-backed tabs pull <c>IconPath</c> and <c>AccentColor</c> from
/// <see cref="UserStoryInteractionVisuals"/> (single source of truth — picks up the teal Following
/// automatically). Labels and slugs are tab-specific (plural forms for Favorites/Private Favorites).
///
/// IconPath values must not change without updating audit/UserStoryInteractions.md Feature 17 first.
/// </summary>
public static class BookshelfTabVisuals
{
    public readonly record struct Info(string IconPath, string AccentColor, string Label, string Slug);

    private const string MyStoriesPath =
        "M2 5H13V21H2Z M13 5H15V21H13Z M17 2L21 6L9 20L5 22L7 18Z";

    private const string ActivelyReadingPath =
        "M1 6H11V20H1Z M13 6H23V20H13Z M3 9.5H9V11H3Z M3 13H9V14.5H3Z M15 9.5H21V11H15Z M15 13H21V14.5H15Z";

    private const string AbandonedPath =
        "M3 13H21V22H3Z M10 17V22H14V17Z M2 13L12 3L22 13Z";

    private static readonly IReadOnlyDictionary<BookshelfTab, Info> Map =
        new Dictionary<BookshelfTab, Info>
        {
            [BookshelfTab.MyStories] = new(
                MyStoriesPath, "#2F7D4F", "My Stories",
                BookshelfTabSlug.For(BookshelfTab.MyStories)),

            [BookshelfTab.HiddenGems] = new(
                RecommendationIcons.HiddenGemIconPath,
                RecommendationIcons.HiddenGemAccentColor,
                "Hidden Gems",
                BookshelfTabSlug.For(BookshelfTab.HiddenGems)),

            [BookshelfTab.Recommendations] = new(
                RecommendationIcons.RecommendationIconPath,
                RecommendationIcons.RecommendationAccentColor,
                "Recommendations",
                BookshelfTabSlug.For(BookshelfTab.Recommendations)),

            [BookshelfTab.Favorites] = new(
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Favorite).IconPath,
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Favorite).AccentColor,
                "Favorites",
                BookshelfTabSlug.For(BookshelfTab.Favorites)),

            [BookshelfTab.PrivateFavorites] = new(
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.PrivateFavorite).IconPath,
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.PrivateFavorite).AccentColor,
                "Private Favorites",
                BookshelfTabSlug.For(BookshelfTab.PrivateFavorites)),

            [BookshelfTab.Completed] = new(
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Complete).IconPath,
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Complete).AccentColor,
                "Completed",
                BookshelfTabSlug.For(BookshelfTab.Completed)),

            [BookshelfTab.Following] = new(
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Follow).IconPath,
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Follow).AccentColor,
                "Following",
                BookshelfTabSlug.For(BookshelfTab.Following)),

            [BookshelfTab.ActivelyReading] = new(
                ActivelyReadingPath, "#2E96A8", "Actively Reading",
                BookshelfTabSlug.For(BookshelfTab.ActivelyReading)),

            [BookshelfTab.ReadItLater] = new(
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.ReadLater).IconPath,
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.ReadLater).AccentColor,
                "Read It Later",
                BookshelfTabSlug.For(BookshelfTab.ReadItLater)),

            [BookshelfTab.Abandoned] = new(
                AbandonedPath, "#9A8580", "Abandoned",
                BookshelfTabSlug.For(BookshelfTab.Abandoned)),

            [BookshelfTab.Ignored] = new(
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Ignore).IconPath,
                UserStoryInteractionVisuals.For(UserStoryInteractionTypeEnum.Ignore).AccentColor,
                "Ignored",
                BookshelfTabSlug.For(BookshelfTab.Ignored)),
        };

    public static Info For(BookshelfTab tab) => Map[tab];

    /// <summary>All 11 tabs in display order.</summary>
    public static IEnumerable<BookshelfTab> AllTabs =>
        Enum.GetValues<BookshelfTab>().OrderBy(t => (int)t);
}
