namespace TheCanalaveLibrary.Core;

/// <summary>
/// One bookable calendar block with its current occupancy — the redemption calendar's row.
/// <see cref="BookedCount"/> is computed by overlap query at read time (the grid is never
/// stored — see <see cref="SpotlightBlocks"/>).
/// </summary>
public record SpotlightBlockDto(
    DateTime StartUtc,
    DateTime EndUtc,
    int BookedCount,
    int Capacity)
{
    public bool HasOpening => BookedCount < Capacity;
}
