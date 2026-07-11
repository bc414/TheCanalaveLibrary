using Microsoft.EntityFrameworkCore;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Hosted driver for the daily <c>site_daily_stats</c> upsert (Feature 62, WU-SiteDailyStat,
/// layer8-data-marts.md). Unlike <see cref="DiscoveryMartWorker"/>, this does not swap staging
/// tables — it upserts one row per completed UTC day via <see cref="SiteDailyStatAggregator"/>.
///
/// At startup, backfills any missing days between the latest existing row (or a bounded lookback
/// if the table is empty) and yesterday — "bounded" so a long-empty table or a fresh deploy
/// doesn't try to reconstruct years of history it has no data for. Then runs once per day at the
/// configured off-hours hour, aggregating the previous completed UTC day.
///
/// A failed upsert is logged as Error and the loop continues — the previous day's rows (which are
/// never touched by a later day's run) keep serving; the next scheduled pass retries today's day.
/// TestAppFactory removes this worker so integration tests upsert deterministically via
/// <see cref="SiteDailyStatAggregator"/> directly (same treatment as the discovery-mart worker).
/// </summary>
public sealed class SiteDailyStatWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SiteDailyStatWorker> logger) : BackgroundService
{
    /// <summary>Startup gap-fill never reaches back further than this many days — a long-empty
    /// table (e.g. first deploy of this feature) has no historical source data to reconstruct
    /// anyway; this bounds the bootstrap to "recently missed days," not "all of history."</summary>
    internal const int MaxBackfillDays = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations, seeding) settle before touching the database.
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            await BackfillMissingDaysAsync(scope, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Site-daily-stat startup backfill failed; daily schedule continues");
        }

        int rebuildHourUtc = configuration.GetValue("Marts:RebuildHourUtc", 3);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(DiscoveryMartWorker.DelayUntilNext(rebuildHourUtc, DateTime.UtcNow), stoppingToken);

                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                SiteDailyStatAggregator aggregator = scope.ServiceProvider.GetRequiredService<SiteDailyStatAggregator>();
                DateOnly yesterday = PreviousCompletedUtcDay(DateTime.UtcNow);
                await aggregator.UpsertDayAsync(yesterday, stoppingToken);
                logger.LogInformation("site_daily_stats upserted for {StatDate}", yesterday);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Previous days' rows are untouched; the next scheduled pass retries today's day.
                logger.LogError(ex, "Daily site_daily_stats upsert failed; will retry next scheduled run");
            }
        }
    }

    private async Task BackfillMissingDaysAsync(AsyncServiceScope scope, CancellationToken ct)
    {
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DateOnly? latestExisting = await context.SiteDailyStats
            .OrderByDescending(s => s.StatDate)
            .Select(s => (DateOnly?)s.StatDate)
            .FirstOrDefaultAsync(ct);

        DateOnly target = PreviousCompletedUtcDay(DateTime.UtcNow);
        List<DateOnly> missingDays = MissingDays(latestExisting, target, MaxBackfillDays);
        if (missingDays.Count == 0) return;

        logger.LogInformation(
            "site_daily_stats backfilling {Count} missing day(s) up to {Target}", missingDays.Count, target);

        SiteDailyStatAggregator aggregator = scope.ServiceProvider.GetRequiredService<SiteDailyStatAggregator>();
        foreach (DateOnly day in missingDays)
            await aggregator.UpsertDayAsync(day, ct);
    }

    /// <summary>The most recent UTC day that has fully elapsed as of <paramref name="nowUtc"/> —
    /// "yesterday" in UTC terms. Public test seam — the repo deliberately has no
    /// InternalsVisibleTo (see <c>ServerWriteRateLimitService</c>/<c>SmtpEmailSender</c>).</summary>
    public static DateOnly PreviousCompletedUtcDay(DateTime nowUtc) =>
        DateOnly.FromDateTime(nowUtc.Date.AddDays(-1));

    /// <summary>
    /// Days strictly after <paramref name="latestExisting"/> (or the bounded lookback window if
    /// the table is empty) up to and including <paramref name="target"/>, capped at
    /// <paramref name="maxBackfillDays"/> entries (the most recent days win — an empty table
    /// backfills only the last <paramref name="maxBackfillDays"/> days, not all of history).
    /// Public test seam — the repo deliberately has no InternalsVisibleTo.
    /// </summary>
    public static List<DateOnly> MissingDays(DateOnly? latestExisting, DateOnly target, int maxBackfillDays)
    {
        DateOnly earliestAllowed = target.AddDays(-(maxBackfillDays - 1));
        DateOnly start = latestExisting is { } existing && existing.AddDays(1) > earliestAllowed
            ? existing.AddDays(1)
            : earliestAllowed;

        var days = new List<DateOnly>();
        for (DateOnly day = start; day <= target; day = day.AddDays(1))
            days.Add(day);
        return days;
    }
}
