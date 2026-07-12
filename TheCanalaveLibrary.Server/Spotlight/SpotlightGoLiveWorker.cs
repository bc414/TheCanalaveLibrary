namespace TheCanalaveLibrary.Server;

/// <summary>
/// Hosted driver for the spotlight go-live sweep (<see cref="SpotlightGoLiveSweeper"/> holds the
/// body — worker/body split per the <c>SiteDailyStatWorker</c>/<c>SiteDailyStatAggregator</c>
/// pattern). A one-minute cadence bounds notification lateness; the sweep query is a single
/// indexed range scan, so the idle cost is negligible. <c>TestAppFactory</c> removes this worker;
/// integration tests call the sweeper directly.
/// </summary>
public sealed class SpotlightGoLiveWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SpotlightGoLiveWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations, seeding) settle before touching the database.
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(SweepInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                SpotlightGoLiveSweeper sweeper = scope.ServiceProvider.GetRequiredService<SpotlightGoLiveSweeper>();
                int stamped = await sweeper.SweepAsync(stoppingToken);
                if (stamped > 0)
                    logger.LogInformation("Spotlight go-live sweep notified {Count} placement(s)", stamped);

                if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The loop survives a failed cycle; unstamped placements retry next tick.
                logger.LogError(ex, "Spotlight go-live sweep failed; retrying next tick");
                try { if (!await timer.WaitForNextTickAsync(stoppingToken)) return; }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
