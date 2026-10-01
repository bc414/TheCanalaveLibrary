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

    /// <summary>
    /// Counter-backed badge key → the <c>user_stats</c> column its <c>UserBadge.EarnedCount</c> mirrors.
    /// The single source for both readers: <c>UserStatRecalculator</c>'s badge sync and the display
    /// filter (<see cref="CounterBackedKeys"/>). Patron/Architect/Artist are manual grants — the grant
    /// row is the fact, with no counter behind it — and are deliberately absent (owner ruling D21).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> CounterColumnByBadge = new Dictionary<string, string>
    {
        [Recommender] = "recommendation_successes_earned",
        [BetaReader] = "acknowledged_as_beta_reader_count",
    };

    /// <summary>
    /// The keys of <see cref="CounterColumnByBadge"/>, as an array EF translates to <c>= ANY</c>.
    /// Display projections drop a counter-backed badge whose <c>EarnedCount</c> is 0 — under the
    /// no-tiers model a badge is a number, and 0 is incoherent — while a manual grant
    /// (<c>EarnedCount = 0</c> by design) always shows. The recalc never deletes the row (D21: hide at
    /// display, keep the row as a diagnostic), and the owner's curation read still returns it.
    /// </summary>
    public static readonly string[] CounterBackedKeys = [.. CounterColumnByBadge.Keys];
}
