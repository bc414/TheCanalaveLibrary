// In a new file, e.g., Data/SiteConstants.cs
// SiteSearchModes and UserStoryInteractionFilters moved to Core/Discovery/SiteSearchModes.cs (WU28)
// so SharedUI components and the service interface can reference them without depending on Server.

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