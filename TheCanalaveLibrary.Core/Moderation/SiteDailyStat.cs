namespace TheCanalaveLibrary.Core;

/// <summary>
/// Feature 62 — one immutable row per completed UTC day of site-wide aggregate counters.
/// Append-only ground truth, <b>not</b> a rebuildable Layer-8 mart (see
/// <c>layer8-data-marts.md</c> §"site_daily_stats" for the full reasoning): unlike the three
/// discovery marts, this table is the one documented Layer-8 exception that gets a normal EF
/// model + migration, because it's low-volume ground truth with rich time-series reads (the
/// <c>/mod/stats</c> dashboard).
///
/// <b>EF owns schema + reads only.</b> <see cref="TheCanalaveLibrary.Server.SiteDailyStatAggregator"/>
/// writes every row via raw <c>INSERT … ON CONFLICT (stat_date) DO UPDATE</c> — never through this
/// context's change tracker. Do not add a service that calls <c>SaveChangesAsync</c> against this
/// DbSet; the upsert must stay idempotent per <c>stat_date</c>, which the change tracker cannot
/// guarantee across retries.
///
/// Columns split into stock snapshots (<c>Total*</c>, as-of end-of-day) and daily flows (event counts
/// for that day). Every column is NOT NULL except <see cref="ActiveUsers"/>, whose NULL means "this day
/// could not be measured" (see its own doc).
/// </summary>
public class SiteDailyStat
{
    /// <summary>The completed UTC day this row summarizes. Primary key.</summary>
    public DateOnly StatDate { get; set; }

    // --- Stock snapshots (as-of end-of-day) ---
    public int TotalUsers { get; set; }
    public int TotalStories { get; set; }
    public long TotalWords { get; set; }

    // --- Daily flows ---
    /// <summary>Users created that day (<see cref="User.CreatedUtc"/>). NOT NULL since
    /// WU-SchemaHardening: <c>created_utc</c> has been in the schema since the 2026-07-18 migration
    /// collapse, so every day is countable.</summary>
    public int NewUsers { get; set; }
    public int NewStories { get; set; }
    public int NewChapters { get; set; }
    public long NewWords { get; set; }
    public int NewComments { get; set; }
    public int NewBlogPosts { get; set; }
    public int NewGroups { get; set; }
    public int NewFollows { get; set; }
    public int NewRecommendationsWritten { get; set; }
    public int NewRecommendationSuccesses { get; set; }
    public int ReportsFiled { get; set; }
    public int ReportsResolved { get; set; }
    public int FavoritesAdded { get; set; }

    /// <summary>Approximate proxy: <c>UserChapterInteraction.LastInteractionDate</c> is a mutable,
    /// overwritten "last touched" stamp, not a per-day event log. Documented as approximate.</summary>
    public int ChaptersRead { get; set; }

    /// <summary>Story reads only (<c>daily_story_stats.view_count</c> SUM) — not site-wide page
    /// views; no such stream exists. Named honestly rather than as the more general "page_views."</summary>
    public long StoryViews { get; set; }

    /// <summary>Users active that day, or null when the day could not be measured. Every ping
    /// overwrites <see cref="User.LastActiveUtc"/>, so a day is countable only while it is the most
    /// recent completed UTC day (or today); an older, gap-filled day is written null rather than a
    /// silent undercount, and a re-upsert never replaces a measured value with null
    /// (<c>SiteDailyStatAggregator.IsActiveUsersMeasurable</c>; layer8-data-marts.md
    /// §"site_daily_stats").</summary>
    public int? ActiveUsers { get; set; }
}
