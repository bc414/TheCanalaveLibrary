using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// <see cref="ImageOrphanSweepWorker"/>'s schedule (owner ruling D14's "log-only first cycle"; WU-BlobCleanup
/// review fixes, 2026-10-13). The worker loop itself has no automated test (<c>TestAppFactory</c> removes
/// it, as every worker), so its one decision is a public static seam: what the next wait is after a cycle.
/// The defect this pins: with the dry run first and the next cycle a full day later, a process restarted
/// at least daily never deleted anything, and D14's backstop was inert. Tier: Unit (static, no host).
/// </summary>
public class ImageOrphanSweepWorkerTests
{
    [Fact]
    public void AProcessThatLivesADay_RunsARealCycle()
    {
        // Start → dry run after StartupDelay → first real cycle after DelayAfterCycle(dry run completed).
        TimeSpan firstRealCycle = ImageOrphanSweepWorker.StartupDelay
                                  + ImageOrphanSweepWorker.DelayAfterCycle(completedDryRun: true);

        firstRealCycle.Should().BeLessThan(TimeSpan.FromHours(24),
            "a deploy, crash or reboot at least once a day must not keep the sweep a dry run forever");
        firstRealCycle.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(1) + ImageOrphanSweepWorker.StartupDelay);
    }

    [Fact]
    public void ACompletedDryRun_IsFollowedByTheFirstRealCycle_NotADay()
    {
        ImageOrphanSweepWorker.DelayAfterCycle(completedDryRun: true)
            .Should().Be(ImageOrphanSweepWorker.FirstRealCycleDelay);
        ImageOrphanSweepWorker.FirstRealCycleDelay.Should().BePositive(
            "the dry run's log must exist before the first delete, so the kill switch can stop a bad predicate");
    }

    [Fact]
    public void EveryOtherCycle_WaitsTheDailyInterval()
    {
        // A real cycle, a cycle the kill switch skipped, and a failed cycle (dry or real) all report
        // "no dry run completed".
        ImageOrphanSweepWorker.DelayAfterCycle(completedDryRun: false)
            .Should().Be(ImageOrphanSweepWorker.SweepInterval);
        ImageOrphanSweepWorker.SweepInterval.Should().Be(TimeSpan.FromHours(24), "D14: a daily sweeper");
    }
}
