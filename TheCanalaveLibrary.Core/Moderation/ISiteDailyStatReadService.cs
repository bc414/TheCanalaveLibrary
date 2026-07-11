namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side for the <c>/mod/stats</c> dashboard (Feature 62 flourish — see
/// layer8-data-marts.md §"site_daily_stats"). Read-only over the <see cref="SiteDailyStat"/> EF
/// model via LINQ — this is the one Layer-8 table with an EF model, precisely so this service can
/// exist without hand-mapping raw SQL rows.
/// </summary>
public interface ISiteDailyStatReadService
{
    /// <summary>The most recent aggregated day, or null if the worker hasn't run yet.</summary>
    Task<SiteDailyStatDto?> GetLatestAsync(CancellationToken ct = default);

    /// <summary>The last <paramref name="days"/> aggregated days, ascending by date (oldest
    /// first — the order charts want). May return fewer than requested if the mart doesn't have
    /// that much history yet.</summary>
    Task<IReadOnlyList<SiteDailyStatDto>> GetSeriesAsync(int days, CancellationToken ct = default);
}
