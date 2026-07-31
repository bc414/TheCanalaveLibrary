namespace TheCanalaveLibrary.Server;

/// <summary>
/// Catalogue key constants (Feature 50). No-tiers model (WU-StatBadgeProducers, 2026-07-30 —
/// supersedes WU36's Bronze/Silver tiers, which had no design provenance; see
/// <c>audit/Badges.md</c> "Tier paradigm — RETIRED site-wide"). A badge is earned at ≥1 and
/// displays its <c>UserBadge.EarnedCount</c> — there is no silver/bronze split.
/// </summary>
public static class SiteBadges
{
    public const string Patron = "Patron";

    /// <summary>Auto-awarded at ≥1 reader confirming a recommendation was genuinely helpful.</summary>
    public const string Recommender = "Recommender";

    /// <summary>Auto-awarded at ≥1 accepted <c>StoryAcknowledgment</c> credit (role Beta Reader).</summary>
    public const string BetaReader = "BetaReader";

    public const string Architect = "Architect";
    public const string Artist = "Artist";
}
