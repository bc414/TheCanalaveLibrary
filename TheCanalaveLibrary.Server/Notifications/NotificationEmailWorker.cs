namespace TheCanalaveLibrary.Server;

/// <summary>
/// Periodic drain worker for the notification-email buffer — the cadence half of the write-behind
/// fan-out (<c>layer2-services.md</c> §"Email fan-out"). Ticks every <see cref="FlushInterval"/>,
/// delegating each cycle to <see cref="NotificationEmailFlusher"/>; a failed cycle logs (the flusher
/// already restored the batch) and the loop continues.
///
/// <para><b>Cadence rationale:</b> 30 seconds. Email is not latency-critical — batching is the
/// entire point, and a longer window means more messages share one SMTP connection. Anything much
/// longer would make the Mailpit verification loop tedious without buying real efficiency.</para>
///
/// <para><b>Only registered on the <c>Email:Provider = "Smtp"</c> branch</b> (Program.cs). An
/// unconfigured host does no drain work at all rather than draining into a sink; the buffer on such
/// a host reports <c>IsEnabled = false</c> and discards at enqueue.</para>
///
/// <para><b>Graceful shutdown:</b> after cancellation the worker drains one final time so a deploy
/// doesn't strand a cycle's worth of queued mail — only a hard crash loses it. That loss is
/// acceptable and bounded by design: the in-app notification is always already durable, so the
/// worst case is a missing email for a notification the user can still see on the site.</para>
///
/// <para>Excluded from the integration-test host (<c>TestAppFactory</c>) — tests flush
/// deterministically via the flusher instead of racing this timer.</para>
/// </summary>
public sealed class NotificationEmailWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationEmailWorker> logger) : BackgroundService
{
    /// <summary>Drain cadence — the upper bound on how long a queued email waits to be sent.</summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

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

        // Graceful-shutdown drain: CancellationToken.None because stoppingToken is already
        // cancelled; the host's shutdown timeout still bounds this.
        await FlushOneCycleAsync(CancellationToken.None);
    }

    private async Task FlushOneCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            NotificationEmailFlusher flusher =
                scope.ServiceProvider.GetRequiredService<NotificationEmailFlusher>();
            await flusher.FlushAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Already logged (with batch size) by the flusher, which also restored the batch.
            // Log at Warning here so the worker loop's survival is itself visible in the timeline.
            logger.LogWarning(ex, "Notification email flush cycle failed; worker continues.");
        }
    }
}
