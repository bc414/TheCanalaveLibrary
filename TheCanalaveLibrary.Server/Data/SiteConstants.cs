// In a new file, e.g., Data/SiteConstants.cs

namespace TheCanalaveLibrary.Server;

public static class SiteBadges
{
    public const string Patron = "Patron";
    /// <summary>Tastemaker tier 1 — 10 readers found your recommendations genuinely helpful.</summary>
    public const string Recommender = "Recommender";
    /// <summary>Tastemaker tier 2 — 50 readers found your recommendations genuinely helpful.</summary>
    public const string RecommenderSilver = "RecommenderSilver";
    public const string BetaReader = "BetaReader";
    public const string Architect = "Architect";
    public const string Artist = "Artist";
}

// Search modes are discovery *surfaces* (pages where different filter defaults make sense), per the
// revised three-axis model (§5.3). "Random Search" is not a mode — it's Source=All + Sort=Random on
// the SearchPage surface.
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

// One key per boolean column on UserStoryInteraction — strictly 1:1, no compounds.
// Compound exclusions (e.g. "actively reading") are expressed by combining these, not by adding a key.
public static class UserStoryInteractionFilters
{
    public const string HasStarted = "HasStarted";
    public const string Completed = "Completed";
    public const string Favorited = "Favorited";
    public const string HiddenFavorited = "HiddenFavorited";
    public const string Followed = "Followed";
    public const string ReadItLater = "ReadItLater";
    public const string Ignored = "Ignored";
}