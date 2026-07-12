using FluentAssertions;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="SpotlightBlocks"/> — the pure block-grid math behind Community
/// Spotlight booking (Feature 55, WU-Spotlight). Tier: Unit (directly constructed, no host/DB).
/// </summary>
public class SpotlightBlocksTests
{
    private static readonly DateTime Epoch = SpotlightBlocks.Epoch;

    // ── FloorToBlockStart ─────────────────────────────────────────────────────────

    [Fact]
    public void FloorToBlockStart_OnBoundary_ReturnsSameInstant()
    {
        DateTime boundary = Epoch.AddDays(21); // three 7-day blocks after the epoch
        SpotlightBlocks.FloorToBlockStart(boundary, 7).Should().Be(boundary);
    }

    [Fact]
    public void FloorToBlockStart_MidBlock_FloorsToBlockStart()
    {
        DateTime midBlock = Epoch.AddDays(10); // inside block [7, 14)
        SpotlightBlocks.FloorToBlockStart(midBlock, 7).Should().Be(Epoch.AddDays(7));
    }

    [Fact]
    public void FloorToBlockStart_JustBeforeBoundary_StaysInPriorBlock()
    {
        DateTime almostNext = Epoch.AddDays(14).AddTicks(-1);
        SpotlightBlocks.FloorToBlockStart(almostNext, 7).Should().Be(Epoch.AddDays(7));
    }

    [Fact]
    public void FloorToBlockStart_PreEpoch_FloorsDownNotTowardZero()
    {
        DateTime beforeEpoch = Epoch.AddDays(-3); // inside block [-7, 0)
        SpotlightBlocks.FloorToBlockStart(beforeEpoch, 7).Should().Be(Epoch.AddDays(-7));
    }

    [Fact]
    public void FloorToBlockStart_RespectsDuration()
    {
        DateTime instant = Epoch.AddDays(5);
        SpotlightBlocks.FloorToBlockStart(instant, 3).Should().Be(Epoch.AddDays(3)); // blocks [3, 6)
    }

    [Fact]
    public void FloorToBlockStart_NonPositiveDuration_Throws()
    {
        Action act = () => SpotlightBlocks.FloorToBlockStart(Epoch, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── IsOnGrid ──────────────────────────────────────────────────────────────────

    [Fact]
    public void IsOnGrid_BoundaryTrue_MidBlockFalse()
    {
        SpotlightBlocks.IsOnGrid(Epoch.AddDays(7), 7).Should().BeTrue();
        SpotlightBlocks.IsOnGrid(Epoch.AddDays(7).AddHours(1), 7).Should().BeFalse();
    }

    [Fact]
    public void Epoch_IsAMonday_SoWeeklyBlocksAlignToCalendarWeeks()
    {
        Epoch.DayOfWeek.Should().Be(DayOfWeek.Monday);
        Epoch.TimeOfDay.Should().Be(TimeSpan.Zero);
        Epoch.Kind.Should().Be(DateTimeKind.Utc);
    }

    // ── BookableBlocks ────────────────────────────────────────────────────────────

    [Fact]
    public void BookableBlocks_FirstBlockIsCurrent_AndContiguous()
    {
        DateTime now = Epoch.AddDays(10); // inside block [7, 14)
        IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> blocks =
            SpotlightBlocks.BookableBlocks(now, 7, 30);

        blocks[0].StartUtc.Should().Be(Epoch.AddDays(7), "the current (partially elapsed) block is bookable");
        blocks[0].EndUtc.Should().Be(Epoch.AddDays(14));
        for (int i = 1; i < blocks.Count; i++)
            blocks[i].StartUtc.Should().Be(blocks[i - 1].EndUtc, "blocks tile with no gaps");
    }

    [Fact]
    public void BookableBlocks_BoundedByHorizon()
    {
        DateTime now = Epoch.AddDays(10);
        IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> blocks =
            SpotlightBlocks.BookableBlocks(now, 7, 30);

        // Every block starts before now+30d; the next one after the last would not.
        blocks.Should().OnlyContain(b => b.StartUtc < now.AddDays(30));
        blocks[^1].EndUtc.Should().BeOnOrAfter(now.AddDays(30));
    }

    [Fact]
    public void BookableBlocks_ZeroHorizon_OffersOnlyTheCurrentBlock()
    {
        // The current block's start is already in the past, so a zero horizon still offers it —
        // and nothing beyond.
        DateTime now = Epoch.AddDays(10); // inside block [7, 14)
        IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> blocks =
            SpotlightBlocks.BookableBlocks(now, 7, 0);

        blocks.Should().ContainSingle().Which.StartUtc.Should().Be(Epoch.AddDays(7));
    }

    [Fact]
    public void BookableBlocks_AllStartsAreOnGrid()
    {
        DateTime now = Epoch.AddDays(23).AddHours(5);
        IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> blocks =
            SpotlightBlocks.BookableBlocks(now, 7, 60);

        blocks.Should().OnlyContain(b => SpotlightBlocks.IsOnGrid(b.StartUtc, 7));
        blocks.Count.Should().BeGreaterThan(7, "a 60-day horizon spans at least 8 weekly blocks");
    }
}
