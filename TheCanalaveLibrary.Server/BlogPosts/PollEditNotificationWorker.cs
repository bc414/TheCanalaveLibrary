namespace TheCanalaveLibrary.Server;

/// <summary>
/// Hosted driver for the poll edit-notification sweep (<see cref="PollEditNotificationSweeper"/>
/// holds the body — worker/body split per the <c>SpotlightGoLiveWorker</c> pattern). A one-minute
/// cadence bounds notification lateness past the 30-minute quiet period; the sweep query is a
/// partial-index range scan (<c>ix_base_polls_last_edited_at</c>), so the idle cost is negligible.
/// <c>TestAppFactory</c> removes this worker; integration tests call the sweeper directly.
/// </summary>
public sealed class PollEditNotificationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PollEditNotificationWorker> logger) : BackgroundService
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
                PollEditNotificationSweeper sweeper =
                    scope.ServiceProvider.GetRequiredService<PollEditNotificationSweeper>();
                int stamped = await sweeper.SweepAsync(stoppingToken);
                if (stamped > 0)
                    logger.LogInformation("Poll edit sweep notified voters of {Count} poll(s)", stamped);

                if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The loop survives a failed cycle; un-notified polls retry next tick.
                logger.LogError(ex, "Poll edit sweep failed; retrying next tick");
                try { if (!await timer.WaitForNextTickAsync(stoppingToken)) return; }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
