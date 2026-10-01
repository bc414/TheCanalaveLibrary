namespace TheCanalaveLibrary.Server;

/// <summary>
/// Hosted driver for the daily counter reconciliation (Feature 58, WU-UserStatRecalc,
/// layer2-services.md "Recalculation worker (F58)"). Each completed off-hours window runs two passes,
/// in this order:
/// <list type="number">
/// <item><see cref="ContentCounterRecalculator"/> — the content counters (likes, successes, versions,
/// story word counts, active report counts; owner ruling D21, WU-CounterSymmetry).</item>
/// <item><see cref="UserStatRecalculator"/> — the <c>user_stats</c> counters and badge counts. It runs
/// second because <c>words_written</c> sums the <c>stories.word_count</c> the first pass corrects.</item>
/// </list>
/// Same cadence source as <see cref="DiscoveryMartWorker"/> and <see cref="SiteDailyStatWorker"/>
/// (<c>Marts:RebuildHourUtc</c>, default 03:00 UTC), deliberately shared rather than a dedicated
/// config key: all three are low-urgency off-hours reconciliation passes with no reason to run at a
/// different hour from each other.
///
/// Each pass has its own try/log: a failed pass is logged as Error and does not block the other, and
/// the loop continues — the previous (possibly drifted) counter values keep serving; the next
/// scheduled pass retries. TestAppFactory removes this worker so integration tests recalculate
/// deterministically via the two recalculators directly (same treatment as the other daily workers).
/// </summary>
public sealed class UserStatRecalculationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<UserStatRecalculationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations, seeding) settle before touching the database.
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        int rebuildHourUtc = configuration.GetValue("Marts:RebuildHourUtc", 3);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(DiscoveryMartWorker.DelayUntilNext(rebuildHourUtc, DateTime.UtcNow), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            if (!await RunContentPassAsync(stoppingToken)) return;
            if (!await RunUserStatPassAsync(stoppingToken)) return;
        }
    }

    /// <summary>The content-counter pass. Returns false only when the host is stopping.</summary>
    private async Task<bool> RunContentPassAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ContentCounterRecalculator recalculator = scope.ServiceProvider.GetRequiredService<ContentCounterRecalculator>();
            ContentCounterRecalcResult result = await recalculator.RecalculateAllAsync(stoppingToken);
            logger.LogInformation(
                "Content counter recalculation completed: {CountersCorrected} counter value(s) corrected",
                result.CountersCorrected);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            // Previous counter values keep serving; the UserStat pass still runs; the next scheduled
            // pass retries this one.
            logger.LogError(ex, "Daily content counter recalculation failed; previous counter values remain live");
            return true;
        }
    }

    /// <summary>The UserStat pass. Returns false only when the host is stopping.</summary>
    private async Task<bool> RunUserStatPassAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            UserStatRecalculator recalculator = scope.ServiceProvider.GetRequiredService<UserStatRecalculator>();
            UserStatRecalcResult result = await recalculator.RecalculateAllAsync(stoppingToken);
            logger.LogInformation(
                "UserStat recalculation completed: {RowsInserted} missing row(s) inserted, {CountersCorrected} counter value(s) corrected",
                result.RowsInserted, result.CountersCorrected);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            // Previous counter values keep serving; the next scheduled pass retries.
            logger.LogError(ex, "Daily UserStat recalculation failed; previous counter values remain live");
            return true;
        }
    }
}
