namespace TheCanalaveLibrary.Server;

/// <summary>
/// Periodic drain worker for the user-activity buffer (WU-SiteDailyStat, Feature 62 L2 — sibling of
/// <see cref="ReadingProgressFlushWorker"/>/<see cref="ViewCountFlushWorker"/>; same cadence,
/// shutdown-drain, and singleton-worker discipline). Excluded from the integration-test host
/// (TestAppFactory) — tests flush deterministically via <see cref="UserActivityFlusher"/>.
/// </summary>
public sealed class UserActivityFlushWorker(
    UserActivityFlusher flusher,
    ILogger<UserActivityFlushWorker> logger) : BackgroundService
{
    /// <summary>Flush cadence — the loss window of the eventually-durable contract.</summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await FlushOneCycleAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown — fall through to the final drain.
        }

        await FlushOneCycleAsync(CancellationToken.None);
    }

    private async Task FlushOneCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await flusher.FlushAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Already logged (with batch size) by the flusher, which also restored the batch.
            logger.LogWarning(ex, "User-activity flush cycle failed; worker continues.");
        }
    }
}
