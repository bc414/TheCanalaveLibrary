namespace TheCanalaveLibrary.Core;

/// <summary>
/// The six tabs on the public profile page (<c>/user/{UserId}/{*Tab}</c>).
/// Members are ordered by display position (tab bar left-to-right).
/// Mirrors <see cref="BookshelfTab"/> / <see cref="BookshelfTabSlug"/> in shape (WU27).
/// <see cref="Series"/> added WU41 (Feature 9) — lists the author's series, public to any viewer.
/// </summary>
public enum ProfileTab
{
    Profile         = 0,   // default — bio + comment wall
    Favorites       = 1,
    Recommendations = 2,
    Authored        = 3,
    Blog            = 4,
    Series          = 5,
}

/// <summary>
/// Maps between <see cref="ProfileTab"/> enum values and their URL slug segments
/// (e.g. <c>ProfileTab.Profile</c> ↔ <c>"profile"</c>).
/// <c>null</c> input (the bare <c>/user/{id}</c> route) maps to the default tab
/// (<see cref="ProfileTab.Profile"/>); unrecognised slugs return <c>null</c> (caller → NotFound).
/// </summary>
public static class ProfileTabSlug
{
    private static readonly IReadOnlyDictionary<ProfileTab, string> TabToSlug =
        new Dictionary<ProfileTab, string>
        {
            [ProfileTab.Profile]         = "profile",
            [ProfileTab.Favorites]       = "favorites",
            [ProfileTab.Recommendations] = "recommendations",
            [ProfileTab.Authored]        = "authored",
            [ProfileTab.Blog]            = "blog",
            [ProfileTab.Series]          = "series",
        };

    private static readonly IReadOnlyDictionary<string, ProfileTab> SlugToTab =
        TabToSlug.ToDictionary(kv => kv.Value, kv => kv.Key);

    /// <summary>Returns the URL slug for a given tab.</summary>
    public static string For(ProfileTab tab) => TabToSlug[tab];

    /// <summary>
    /// Parses a URL segment to a <see cref="ProfileTab"/>.
    /// <c>null</c>/empty → <see cref="ProfileTab.Profile"/> (default tab for bare route).
    /// Unrecognised slug → <c>null</c> (caller should call <c>Nav.NotFound()</c>).
    /// </summary>
    public static ProfileTab? Parse(string? slug) =>
        string.IsNullOrEmpty(slug)
            ? ProfileTab.Profile
            : SlugToTab.TryGetValue(slug, out var tab)
                ? tab
                : null;
}
