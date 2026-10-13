namespace TheCanalaveLibrary.Server;

/// <summary>
/// Hosted driver for the daily orphan-blob sweep (<see cref="ImageOrphanSweeper"/> holds the body —
/// the <c>NotificationCleanupWorker</c> shape: 5 s startup delay, a 24 h cadence, an Error log and
/// survive on a failed cycle). <c>TestAppFactory</c> removes this worker; integration tests call the
/// sweeper directly.
///
/// <para><b>Each process start runs one log-only cycle, and the first real cycle follows it
/// <see cref="FirstRealCycleDelay"/> later</b> (owner ruling D14's "log-only first cycle"): the dry run
/// logs what it would delete, so the would-be deletes of a new build's predicate are on record, and the
/// kill switch can stop it, before anything is deleted. Real cycles then run every
/// <see cref="SweepInterval"/>. Derived, not owner text (WU-BlobCleanup review fixes, 2026-10-13): the
/// dry-run state is per process, because persisting it would need a table for one flag, and the real
/// cycle comes an hour after it rather than a day, because a process that is restarted at least daily
/// (a deploy, a crash, a droplet reboot) would otherwise never delete anything and leave the backstop
/// inert. What remains: a process restarted more often than hourly only ever dry-runs.</para>
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
    /// <summary>Lets startup (migrations, seeding) settle before the first cycle touches the database.</summary>
    public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);

    /// <summary>The cadence of real cycles (D14: a daily sweeper).</summary>
    public static readonly TimeSpan SweepInterval = TimeSpan.FromHours(24);

    /// <summary>The gap between a completed dry run and the first real cycle. Long enough to read the
    /// dry run's log after a deploy and flip the kill switch; short enough that a process restarted
    /// daily still deletes.</summary>
    public static readonly TimeSpan FirstRealCycleDelay = TimeSpan.FromHours(1);

    /// <summary>The kill-switch key; absent means enabled.</summary>
    public const string EnabledKey = "ImageStorage:OrphanSweep:Enabled";

    /// <summary>
    /// The wait before the next cycle. Only a dry run that completed is followed by
    /// <see cref="FirstRealCycleDelay"/>; a real cycle, a cycle the kill switch skipped and a failed
    /// cycle (dry or real) wait the full <see cref="SweepInterval"/>. Public test seam — the repo
    /// deliberately has no InternalsVisibleTo (see <c>SiteDailyStatWorker</c>).
    /// </summary>
    public static TimeSpan DelayAfterCycle(bool completedDryRun) =>
        completedDryRun ? FirstRealCycleDelay : SweepInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Set only once a dry run has actually completed: a cycle skipped by the kill switch, or one that
        // failed, is not the log-only first cycle.
        bool dryRunDone = false;
        TimeSpan delay = StartupDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            bool completedDryRun = false;
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
                    bool dryRun = !dryRunDone;
                    await sweeper.SweepAsync(dryRun, stoppingToken);
                    dryRunDone = true;
                    completedDryRun = dryRun;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // The loop survives a failed cycle; orphans are re-swept next tick.
                logger.LogError(ex, "Orphan sweep failed; retrying next tick");
            }

            delay = DelayAfterCycle(completedDryRun);
        }
    }
}
