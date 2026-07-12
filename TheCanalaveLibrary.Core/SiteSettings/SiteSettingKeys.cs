namespace TheCanalaveLibrary.Core;

/// <summary>
/// The catalog of <see cref="SiteSetting"/> keys: each key constant is paired with its default so
/// the EF seed (<c>SiteSettingsConfigurations</c>) and the read-service fallback reference the
/// same value — one source of truth; a missing or unparseable row degrades to the default
/// instead of throwing. Add a key here + a seed row when a feature mints a new knob.
/// </summary>
public static class SiteSettingKeys
{
    // ── Community Spotlight (Feature 55, WU-Spotlight — audit/Spotlight.md) ─────────

    /// <summary>Days per booking block on the spotlight calendar grid.</summary>
    public const string SpotlightBlockDurationDays = "Spotlight.BlockDurationDays";
    public const int SpotlightBlockDurationDaysDefault = 7;

    /// <summary>Concurrent homepage spotlight positions (block capacity). Mod-set now; the
    /// activity/cost-scaled formula is deferred with the donation pipeline.</summary>
    public const string SpotlightPositionCount = "Spotlight.PositionCount";
    public const int SpotlightPositionCountDefault = 3;

    /// <summary>Days after a spotlight ends (and before one starts) during which the same story
    /// cannot be spotlighted again.</summary>
    public const string SpotlightCooldownDays = "Spotlight.CooldownDays";
    public const int SpotlightCooldownDaysDefault = 90;

    /// <summary>How far ahead (days from now) a slot holder may book a block.</summary>
    public const string SpotlightBookingHorizonDays = "Spotlight.BookingHorizonDays";
    public const int SpotlightBookingHorizonDaysDefault = 60;

    /// <summary>Maximum slot grants per calendar (UTC) month — models "donations are capped for
    /// the month by actual site costs, so slots stay meaningful and proportional."</summary>
    public const string SpotlightMonthlyGrantCap = "Spotlight.MonthlyGrantCap";
    public const int SpotlightMonthlyGrantCapDefault = 12;

    /// <summary>All seeded (key, default) pairs — drives the EF <c>HasData</c> seed.</summary>
    public static readonly IReadOnlyList<(string Key, string DefaultValue)> Seed =
    [
        (SpotlightBlockDurationDays, SpotlightBlockDurationDaysDefault.ToString()),
        (SpotlightPositionCount, SpotlightPositionCountDefault.ToString()),
        (SpotlightCooldownDays, SpotlightCooldownDaysDefault.ToString()),
        (SpotlightBookingHorizonDays, SpotlightBookingHorizonDaysDefault.ToString()),
        (SpotlightMonthlyGrantCap, SpotlightMonthlyGrantCapDefault.ToString()),
    ];
}
