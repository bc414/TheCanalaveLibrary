namespace TheCanalaveLibrary.Server;

/// <summary>
/// Hosted driver for the daily UserStat recalculation (Feature 58, WU-UserStatRecalc,
/// layer2-services.md "Recalculation worker (F58)"). One <see cref="UserStatRecalculator"/> pass
/// per completed off-hours window — same cadence source as <see cref="DiscoveryMartWorker"/> and
/// <see cref="SiteDailyStatWorker"/> (<c>Marts:RebuildHourUtc</c>, default 03:00 UTC), deliberately
/// shared rather than a dedicated config key: all three are low-urgency off-hours reconciliation
/// passes with no reason to run at a different hour from each other.
///
/// A failed pass is logged as Error and the loop continues — the previous (possibly drifted)
/// counter values keep serving; the next scheduled pass retries. TestAppFactory removes this
/// worker so integration tests recalculate deterministically via <see cref="UserStatRecalculator"/>
/// directly (same treatment as the other daily workers).
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

                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                UserStatRecalculator recalculator = scope.ServiceProvider.GetRequiredService<UserStatRecalculator>();
                UserStatRecalcResult result = await recalculator.RecalculateAllAsync(stoppingToken);
                logger.LogInformation(
                    "UserStat recalculation completed: {RowsInserted} missing row(s) inserted, {CountersCorrected} counter value(s) corrected",
                    result.RowsInserted, result.CountersCorrected);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Previous counter values keep serving; the next scheduled pass retries.
                logger.LogError(ex, "Daily UserStat recalculation failed; previous counter values remain live");
            }
        }
    }
}
