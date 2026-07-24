namespace TheCanalaveLibrary.Core;

/// <summary>
/// <b>The seam</b> between "who may spotlight" and "what a spotlight is" (settled 2026-07-11 —
/// <c>audit/Spotlight.md</c>): slots are granted here, redeemed via
/// <see cref="ISpotlightWriteService"/>. Today the only grant source is a moderator award
/// (<see cref="SpotlightSlotSource.ModAward"/>, from <c>/mod/spotlight</c>); the deferred
/// donation/payment pipeline becomes a second caller with
/// <see cref="SpotlightSlotSource.Donation"/> — same contract, no redemption-side change.
/// </summary>
public interface ISpotlightSlotAllocator
{
    /// <summary>
    /// Grants one slot to <paramref name="toUserId"/>. <see cref="SpotlightSlotSource.ModAward"/>
    /// requires a moderator caller and respects the monthly grant cap
    /// (<c>Spotlight.MonthlyGrantCap</c> — models "donations are capped for the month by actual
    /// site costs"); <see cref="SpotlightSlotSource.Donation"/> throws until the payment pipeline
    /// lands. Sends <c>SpotlightSlotGranted</c> best-effort post-commit. Returns the new slot id.
    /// </summary>
    /// <param name="toUserId">The awardee.</param>
    /// <param name="source">Grant source (ModAward today).</param>
    /// <param name="maxStoryRating"><see cref="Rating.E"/> (default) = non-M slot;
    /// <see cref="Rating.M"/> = Mature-pool slot (WU-AccessGate dedicated pools).</param>
    Task<int> GrantSlotAsync(int toUserId, SpotlightSlotSource source, Rating maxStoryRating = Rating.E);

    /// <summary>Moderator withdraws an <c>Available</c> (unredeemed) slot — the escape hatch for
    /// expiry-less grants. Throws <see cref="SpotlightValidationException"/> if already redeemed.</summary>
    Task RevokeSlotAsync(int slotId);

    /// <summary>Grants remaining in the current UTC calendar month (cap − non-revoked grants),
    /// floored at 0.</summary>
    Task<int> GetRemainingMonthlyGrantCapacityAsync();

    /// <summary>Most recent grants for the mod surface, newest first. Moderator-only.</summary>
    Task<IReadOnlyList<SpotlightSlotAdminDto>> GetRecentGrantsAsync(int take = 50);
}
