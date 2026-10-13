namespace TheCanalaveLibrary.Server;

/// <summary>
/// Hosted driver for the daily orphan-blob sweep (<see cref="ImageOrphanSweeper"/> holds the body —
/// the <c>NotificationCleanupWorker</c> shape: 5 s startup delay, 24 h <see cref="PeriodicTimer"/>, an
/// Error log and survive on a failed cycle). <c>TestAppFactory</c> removes this worker; integration
/// tests call the sweeper directly.
///
/// <para><b>The first cycle after each process start is a dry run</b> (owner ruling D14's "log-only
/// first cycle"): it logs what it would delete and deletes nothing; later cycles delete. Per process,
/// because that needs no stored state and errs conservative. Consequence: a process that never lives
/// 24 h — a dev server, or a deploy cadence faster than daily — only ever dry-runs.</para>
///
/// <para><b>Kill switch:</b> <c>ImageStorage:OrphanSweep:Enabled</c> (default <c>true</c>), read every
/// cycle. Turn it off during and after a database restore, until the restored rows and the live bucket
/// are reconciled: every blob written after the backup looks orphaned to the restored database
/// (`roadmap.md` Phase 7, "Backups you have restored").</para>
/// </summary>
public sealed class ImageOrphanSweepWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ImageOrphanSweepWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromHours(24);

    /// <summary>The kill-switch key; absent means enabled.</summary>
    public const string EnabledKey = "ImageStorage:OrphanSweep:Enabled";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup (migrations, seeding) settle before touching the database.
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        // Set only once a dry run has actually run: a cycle skipped by the kill switch is not the
        // log-only first cycle.
        bool dryRunDone = false;

        using var timer = new PeriodicTimer(SweepInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!configuration.GetValue(EnabledKey, defaultValue: true))
                {
                    logger.LogInformation("Orphan sweep skipped: {SettingKey} is false", EnabledKey);
                }
                else
                {
                    await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                    ImageOrphanSweeper sweeper = scope.ServiceProvider.GetRequiredService<ImageOrphanSweeper>();
                    await sweeper.SweepAsync(dryRun: !dryRunDone, stoppingToken);
                    dryRunDone = true;
                }

                if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The loop survives a failed cycle; orphans are re-swept next tick.
                logger.LogError(ex, "Orphan sweep failed; retrying next tick");
                try { if (!await timer.WaitForNextTickAsync(stoppingToken)) return; }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
