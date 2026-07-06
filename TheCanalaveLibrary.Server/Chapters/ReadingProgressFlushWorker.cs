using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Periodic drain worker for the reading-progress buffer (Feature 44 L2 — the signal-buffering
/// pattern's cadence half). Ticks every <see cref="FlushInterval"/>, delegating each cycle to
/// <see cref="ReadingProgressFlusher"/>; a failed cycle logs (the flusher already restored the
/// batch) and the loop continues.
///
/// <b>Singleton-worker discipline:</b> exactly one instance per web process. At N≥2 nodes each
/// node drains only its own in-process buffer, which stays correct (per-node coalescing) — global
/// cross-node coalescing is what the deferred shared-store swap buys.
///
/// <b>Graceful shutdown:</b> after cancellation the worker drains one final time so a deploy
/// doesn't eat the loss window — only a hard crash loses the last interval's pings.
///
/// Excluded from the integration-test host (TestAppFactory) — tests flush deterministically via
/// the flusher instead of racing this timer.
/// </summary>
public sealed class ReadingProgressFlushWorker(
    ReadingProgressFlusher flusher,
    ILogger<ReadingProgressFlushWorker> logger) : BackgroundService
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

        // Graceful-shutdown drain: CancellationToken.None because stoppingToken is already
        // cancelled; the host's shutdown timeout still bounds this.
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
            // Log at Warning here so the worker loop's survival is itself visible in the timeline.
            logger.LogWarning(ex, "Reading-progress flush cycle failed; worker continues.");
        }
    }
}
