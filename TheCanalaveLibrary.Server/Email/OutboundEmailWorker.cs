namespace TheCanalaveLibrary.Server;

/// <summary>
/// Periodic drain worker for <see cref="OutboundEmailBuffer"/> — the cadence half of the write-behind
/// outbound email pipeline (<c>layer2-services.md</c> §"Outbound email pipeline"). Ticks every
/// <see cref="FlushInterval"/>, delegating each cycle to <see cref="OutboundEmailFlusher"/>; a failed
/// cycle logs (the flusher already restored the unsent items) and the loop continues. One worker for
/// every kind of mail (notifications and private messages since WU-PrivateMessageEmail, 2026-10-13,
/// which moved it here from <c>Notifications/</c> under its notification name).
///
/// <para><b>Cadence rationale:</b> 30 seconds. Email is not latency-critical — batching is the
/// entire point, and a longer window means more messages share one SMTP connection. Anything much
/// longer would make the Mailpit verification loop tedious without buying real efficiency. Each tick
/// moves at most one batch (<see cref="OutboundEmailFlusher.MaxBatchSize"/>): bounded work per
/// cycle is deliberate. Private-message email inherits it rather than adding a second knob (owner
/// ruling D18 left the interval to the build): a message waits 0–30 s for the next tick, so a
/// recipient who reads it before then is never mailed.</para>
///
/// <para><b>Only registered on the <c>Email:Provider = "Smtp"</c> branch</b> (Program.cs). An
/// unconfigured host does no drain work at all rather than draining into a sink; the buffer on such
/// a host reports <c>IsEnabled = false</c> and discards at enqueue.</para>
///
/// <para><b>Graceful shutdown (WU-NotificationCorrectness, 2026-10-12):</b> after cancellation the
/// worker drains the buffer <em>to empty</em> within <see cref="ShutdownDrainBudget"/>, via
/// <see cref="OutboundEmailFlusher.DrainAllAsync"/> — one final flush used to move only one batch,
/// stranding everything past the first 200 on every deploy. Whatever is still queued when the budget
/// runs out, after a failed batch, or because the server keeps deferring it, is logged as a Warning
/// with its count. A hard crash still loses the in-memory buffer. That loss is acceptable and bounded
/// by design: every producer's row is already durable (the in-app notification, the message itself),
/// so the worst case is a missing email for something the user can still see on the site.</para>
///
/// <para>Excluded from the integration-test host (<c>TestAppFactory</c>) — tests flush and drain
/// deterministically via the flusher instead of racing this timer.</para>
/// </summary>
public sealed class OutboundEmailWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboundEmailWorker> logger) : BackgroundService
{
    /// <summary>
    /// Drain cadence. A queued email normally waits at most this long; when more than one batch is
    /// queued, the tail waits one interval per batch ahead of it (⌈depth / 200⌉ intervals).
    /// </summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the shutdown drain may run. Half of the host's default 30 s
    /// <c>HostOptions.ShutdownTimeout</c> (none is configured in Program.cs). Hosted services stop
    /// sequentially in reverse registration order and this worker is registered first, so it stops
    /// <b>last</b> — after the signal-buffer and poll-edit drains that may still enqueue mail — and the
    /// budget leaves the rest of the timeout for the host itself.
    /// </summary>
    public static readonly TimeSpan ShutdownDrainBudget = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations, seeding) settle before touching the database.
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { /* shutdown during startup delay — still drain below */ }

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

        await DrainOnShutdownAsync();
    }

    private async Task FlushOneCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            OutboundEmailFlusher flusher = scope.ServiceProvider.GetRequiredService<OutboundEmailFlusher>();
            await flusher.FlushAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Already logged (with batch size) by the flusher, which also restored the unsent items.
            // Log at Warning here so the worker loop's survival is itself visible in the timeline.
            logger.LogWarning(ex, "Outbound email flush cycle failed; worker continues.");
        }
    }

    /// <summary>
    /// Graceful-shutdown drain: a fresh budget (<c>stoppingToken</c> is already cancelled), drain to
    /// empty, and report what could not be sent.
    /// </summary>
    private async Task DrainOnShutdownAsync()
    {
        try
        {
            using var budget = new CancellationTokenSource(ShutdownDrainBudget);
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            OutboundEmailFlusher flusher = scope.ServiceProvider.GetRequiredService<OutboundEmailFlusher>();

            int stranded = await flusher.DrainAllAsync(budget.Token);
            if (stranded > 0)
            {
                logger.LogWarning(
                    "Shutdown drain left {StrandedCount} outbound email(s) unsent; they are lost with this process (the rows they announced are durable).",
                    stranded);
            }
        }
        catch (Exception ex)
        {
            // DrainAllAsync never throws; this covers resolving the flusher during host teardown.
            logger.LogWarning(ex, "Outbound email shutdown drain could not run.");
        }
    }
}
