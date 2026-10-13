using FluentAssertions;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="SiteDailyStatWorker"/>'s pure day-window boundary logic (Feature 62,
/// WU-SiteDailyStat) — "previous completed UTC day" selection and the bounded startup gap-fill's
/// missing-days computation. Tier: Unit (static methods, no host/DB — mirrors
/// <see cref="DiscoveryMartWorker.DelayUntilNext"/>'s internal-static testing pattern).
/// </summary>
public class SiteDailyStatWorkerTests
{
    [Fact]
    public void PreviousCompletedUtcDay_IsYesterday()
    {
        DateTime nowUtc = new(2026, 7, 11, 14, 30, 0, DateTimeKind.Utc);

        SiteDailyStatWorker.PreviousCompletedUtcDay(nowUtc)
            .Should().Be(new DateOnly(2026, 7, 10));
    }

    [Fact]
    public void PreviousCompletedUtcDay_JustAfterMidnight_IsStillYesterday()
    {
        DateTime nowUtc = new(2026, 7, 11, 0, 0, 1, DateTimeKind.Utc);

        SiteDailyStatWorker.PreviousCompletedUtcDay(nowUtc)
            .Should().Be(new DateOnly(2026, 7, 10));
    }

    // ── SiteDailyStatAggregator.IsActiveUsersMeasurable (WU-SchemaHardening) ─────────
    // last_active_utc keeps only each user's latest ping, so only the most recent completed day (or
    // today) can be counted; an older, gap-filled day is written NULL instead of an undercount.

    [Theory]
    [InlineData(0, true)]   // today (the dev probe's partial day)
    [InlineData(-1, true)]  // yesterday: the worker's daily target
    [InlineData(-2, false)] // a gap-filled older day
    [InlineData(-30, false)]
    public void IsActiveUsersMeasurable_OnlyTheMostRecentCompletedDayOrToday(int dayOffset, bool expected)
    {
        DateTime nowUtc = new(2026, 7, 11, 14, 30, 0, DateTimeKind.Utc);
        DateOnly statDate = DateOnly.FromDateTime(nowUtc).AddDays(dayOffset);

        SiteDailyStatAggregator.IsActiveUsersMeasurable(statDate, nowUtc).Should().Be(expected);
    }

    [Fact]
    public void IsActiveUsersMeasurable_JustAfterMidnight_YesterdayIsStillMeasurable()
    {
        DateTime nowUtc = new(2026, 7, 11, 0, 0, 1, DateTimeKind.Utc);

        SiteDailyStatAggregator.IsActiveUsersMeasurable(new DateOnly(2026, 7, 10), nowUtc).Should().BeTrue();
        SiteDailyStatAggregator.IsActiveUsersMeasurable(new DateOnly(2026, 7, 9), nowUtc).Should().BeFalse();
    }

    [Fact]
    public void MissingDays_EmptyTable_BackfillsOnlyTheBoundedWindow()
    {
        var target = new DateOnly(2026, 7, 10);

        List<DateOnly> days = SiteDailyStatWorker.MissingDays(latestExisting: null, target, maxBackfillDays: 5);

        days.Should().HaveCount(5);
        days.First().Should().Be(new DateOnly(2026, 7, 6));
        days.Last().Should().Be(target);
    }

    [Fact]
    public void MissingDays_ExistingRow_OnlyFillsAfterIt()
    {
        var latest = new DateOnly(2026, 7, 8);
        var target = new DateOnly(2026, 7, 10);

        List<DateOnly> days = SiteDailyStatWorker.MissingDays(latest, target, maxBackfillDays: 30);

        days.Should().Equal(new DateOnly(2026, 7, 9), new DateOnly(2026, 7, 10));
    }

    [Fact]
    public void MissingDays_UpToDate_ReturnsEmpty()
    {
        var target = new DateOnly(2026, 7, 10);

        List<DateOnly> days = SiteDailyStatWorker.MissingDays(target, target, maxBackfillDays: 30);

        days.Should().BeEmpty();
    }

    [Fact]
    public void MissingDays_LongGap_CapsAtMaxBackfillDays_NotAllOfHistory()
    {
        var latest = new DateOnly(2020, 1, 1); // long-empty table scenario
        var target = new DateOnly(2026, 7, 10);

        List<DateOnly> days = SiteDailyStatWorker.MissingDays(latest, target, maxBackfillDays: 30);

        days.Should().HaveCount(30, "an ancient last row must not trigger a years-long backfill");
        days.Last().Should().Be(target);
    }
}
