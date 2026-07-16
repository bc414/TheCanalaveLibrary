namespace TheCanalaveLibrary.Server;

/// <summary>
/// Hosted driver for the notification cleanup sweep (<see cref="NotificationCleanupSweeper"/>
/// holds the body — worker/body split per the <c>SpotlightGoLiveWorker</c>/<c>SpotlightGoLiveSweeper</c>
/// pattern). A daily cadence is plenty for a 60-day-retention prune; the sweep is one set-based
/// <c>DELETE</c>, so a missed cycle just means slightly more rows next time. <c>TestAppFactory</c>
/// removes this worker; integration tests call the sweeper directly.
/// </summary>
public sealed class NotificationCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationCleanupWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromHours(24);

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
                NotificationCleanupSweeper sweeper =
                    scope.ServiceProvider.GetRequiredService<NotificationCleanupSweeper>();
                await sweeper.SweepAsync(stoppingToken);

                if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The loop survives a failed cycle; eligible rows are re-swept next tick.
                logger.LogError(ex, "Notification cleanup sweep failed; retrying next tick");
                try { if (!await timer.WaitForNextTickAsync(stoppingToken)) return; }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
