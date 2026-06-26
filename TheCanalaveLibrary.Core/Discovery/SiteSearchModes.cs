namespace TheCanalaveLibrary.Core;

/// <summary>
/// Discovery surface keys used in the §8.7 default-settings matrix
/// (<see cref="DefaultUserStoryInteractionFilterSetting"/>).
/// Each key identifies a page where different interaction-filter defaults make sense.
///
/// "RandomSearch" is not a mode — it's Source=All + Sort=Random on <see cref="SearchPage"/>.
/// </summary>
public static class SiteSearchModes
{
    public const string SearchPage = "SearchPage";
    public const string TreeSearch = "TreeSearch";
    public const string AutoTreeSearch = "AutoTreeSearch";
    public const string AlsoFavorited = "AlsoFavorited";
    public const string AlsoRecommended = "AlsoRecommended";
    public const string ProfilePublishedStories = "ProfilePublishedStories";
    public const string ProfileFavorites = "ProfileFavorites";
    public const string ProfileRecommendations = "ProfileRecommendations";
}

/// <summary>
/// Filter key strings for the §8.7 matrix — one key per boolean column on
/// <c>UserStoryInteraction</c>, strictly 1:1, no compounds.
/// Compound exclusions (e.g. "actively reading") are expressed by combining these keys.
/// </summary>
public static class UserStoryInteractionFilters
{
    /// <summary>
    /// The viewer has started the story (<c>has_started</c>). Exists in the catalog but has
    /// no <see cref="UserStoryInteractionTypeEnum"/> counterpart; panel-unexposable.
    /// </summary>
    public const string HasStarted = "HasStarted";
    public const string Completed = "Completed";
    public const string Favorited = "Favorited";
    public const string HiddenFavorited = "HiddenFavorited";
    public const string Followed = "Followed";
    public const string ReadItLater = "ReadItLater";
    public const string Ignored = "Ignored";
}
