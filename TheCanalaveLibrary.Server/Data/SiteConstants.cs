// In a new file, e.g., Data/SiteConstants.cs

namespace TheCanalaveLibrary.Server.Data;

public static class SiteBadges
{
    public const string Patron = "Patron";
    public const string Recommender = "Recommender";
    public const string BetaReader = "BetaReader";
    public const string Architect = "Architect";
    public const string Artist = "Artist";
}

public static class SiteSearchModes
{
    public const string DefaultSearch = "DefaultSearch"; //The regular search with tags
    public const string TreeSearch = "TreeSearch";
    public const string RandomSearch = "RandomSearch";
    public const string AlsoFavorited = "AlsoFavorited";
}

public static class UserStoryInteractionFilters
{
    public const string Ignored = "Ignored";
    public const string Completed = "Completed";
    public const string ReadItLater = "ReadItLater";
    public const string InProgress = "InProgress";
    public const string Favorited = "Favorited";
    public const string HiddenFavorited = "HiddenFavorited";
    public const string Followed = "Followed";
    // ... etc.
}