namespace TheCanalaveLibrary.Core;

/// <summary>
/// The 11 personal reading-management tabs on the Bookshelves page (spec §5.15).
/// Members are ordered by display position (used for both desktop tab bar and mobile dropdown).
/// </summary>
public enum BookshelfTab
{
    MyStories = 0,
    HiddenGems = 1,
    Recommendations = 2,
    Favorites = 3,
    PrivateFavorites = 4,
    Completed = 5,
    Following = 6,
    ActivelyReading = 7,
    ReadItLater = 8,
    Abandoned = 9,
    Ignored = 10,
}

/// <summary>
/// Maps between <see cref="BookshelfTab"/> enum values and their URL slug segments
/// (e.g. <c>BookshelfTab.MyStories</c> ↔ <c>"my-stories"</c>).
/// </summary>
public static class BookshelfTabSlug
{
    private static readonly IReadOnlyDictionary<BookshelfTab, string> TabToSlug =
        new Dictionary<BookshelfTab, string>
        {
            [BookshelfTab.MyStories] = "my-stories",
            [BookshelfTab.HiddenGems] = "hidden-gems",
            [BookshelfTab.Recommendations] = "recommendations",
            [BookshelfTab.Favorites] = "favorites",
            [BookshelfTab.PrivateFavorites] = "private-favorites",
            [BookshelfTab.Completed] = "completed",
            [BookshelfTab.Following] = "following",
            [BookshelfTab.ActivelyReading] = "actively-reading",
            [BookshelfTab.ReadItLater] = "read-it-later",
            [BookshelfTab.Abandoned] = "abandoned",
            [BookshelfTab.Ignored] = "ignored",
        };

    private static readonly IReadOnlyDictionary<string, BookshelfTab> SlugToTab =
        TabToSlug.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static string For(BookshelfTab tab) => TabToSlug[tab];

    /// <summary>
    /// Parses a URL slug to a <see cref="BookshelfTab"/>.
    /// Returns <c>null</c> for unrecognised slugs (caller should navigate to NotFound).
    /// </summary>
    public static BookshelfTab? Parse(string? slug) =>
        slug is not null && SlugToTab.TryGetValue(slug, out var tab) ? tab : null;
}
