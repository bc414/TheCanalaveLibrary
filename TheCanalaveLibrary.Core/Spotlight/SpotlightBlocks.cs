namespace TheCanalaveLibrary.Core;

/// <summary>
/// Pure block-grid math for Community Spotlight bookings (dependency-free Core helper — the
/// <c>ChapterText.CountWords</c> precedent; unit-tested directly, no host or DbContext).
///
/// <para>Time tiles into fixed-duration blocks anchored at <see cref="Epoch"/> (a Monday, so the
/// default 7-day duration aligns blocks to calendar weeks). The grid is <b>computed, never
/// stored</b>: a placement records only its concrete <c>StartDate</c>/<c>EndDate</c>, so changing
/// the block-duration or position-count settings never rewrites data — it only changes which
/// future windows are offered. Capacity per block is the <c>Spotlight.PositionCount</c> site
/// setting, checked at booking time by counting overlapping placements.</para>
/// </summary>
public static class SpotlightBlocks
{
    /// <summary>Grid anchor: Monday 2026-01-05 00:00 UTC. Fixed forever — moving it would shift
    /// every future block boundary under existing bookings.</summary>
    public static readonly DateTime Epoch = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The start of the block containing <paramref name="utc"/> (floor to grid).</summary>
    public static DateTime FloorToBlockStart(DateTime utc, int blockDurationDays)
    {
        if (blockDurationDays < 1)
            throw new ArgumentOutOfRangeException(nameof(blockDurationDays));

        TimeSpan sinceEpoch = utc - Epoch;
        long blockTicks = TimeSpan.FromDays(blockDurationDays).Ticks;
        long index = sinceEpoch.Ticks / blockTicks;
        if (sinceEpoch.Ticks < 0 && sinceEpoch.Ticks % blockTicks != 0)
            index--; // floor, not truncate, for pre-epoch instants
        return Epoch.AddTicks(index * blockTicks);
    }

    /// <summary>True when <paramref name="utc"/> lies exactly on a block boundary of this grid.</summary>
    public static bool IsOnGrid(DateTime utc, int blockDurationDays) =>
        FloorToBlockStart(utc, blockDurationDays) == utc;

    /// <summary>
    /// The bookable windows as of <paramref name="nowUtc"/>: the current block (starts
    /// immediately — its remaining span is partial) followed by every future block whose start
    /// falls within <paramref name="horizonDays"/> of now.
    /// </summary>
    public static IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> BookableBlocks(
        DateTime nowUtc, int blockDurationDays, int horizonDays)
    {
        if (horizonDays < 0) throw new ArgumentOutOfRangeException(nameof(horizonDays));

        var blocks = new List<(DateTime, DateTime)>();
        DateTime start = FloorToBlockStart(nowUtc, blockDurationDays);
        DateTime horizonEnd = nowUtc.AddDays(horizonDays);
        while (start < horizonEnd)
        {
            blocks.Add((start, start.AddDays(blockDurationDays)));
            start = start.AddDays(blockDurationDays);
        }
        return blocks;
    }
}
