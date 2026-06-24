namespace TheCanalaveLibrary.Core;

public static class RecommendationConstants
{
    /// <summary>Minimum plain-text character count (HTML-stripped) for a recommendation body.</summary>
    public const int MinLength = 500;

    /// <summary>Maximum Hidden Gem designations a single user may have active at once. Reject-at-limit.</summary>
    public const int MaxHiddenGemsPerUser = 5;

    /// <summary>Maximum spotlighted ("Author's Pick") recommendations a story author may highlight at once.</summary>
    public const int MaxHighlightedPerStory = 5;
}
