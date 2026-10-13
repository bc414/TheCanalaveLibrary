using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Returns a deleted story's unstarted Spotlight slots to their sponsors — owner ruling D15's sub-ruling
/// (WU-AuthorStoryDelete, 2026-10-13): "If the booked block has not started, the slot returns to
/// redeemable; if it has already aired, it is consumed." A placement cascades off its story, but the
/// slot is a third party's entitlement, not a record, so an author's unilateral delete must not consume
/// it silently. Rule: <c>layer2-services.md</c> §"Community Spotlight" → "Slot return when a story is
/// deleted".
/// <para>
/// <b>Server-only, not on <see cref="ISpotlightWriteService"/>.</b> That interface is WASM-registered;
/// whether a server-only member may sit on one is worksheet D37 (pending), so this stays a plain scoped
/// class with no endpoint and no client twin.
/// </para>
/// <para>
/// <b>Shares the caller's write context,</b> so it runs inside the caller's transaction, and the caller
/// must delete the story in that same transaction: the release only flips slot status, and the
/// placement rows go with the story's cascade.
/// </para>
/// </summary>
public sealed class SpotlightPlacementRelease(ApplicationDbContext writeDb)
{
    /// <summary>
    /// Takes the booking lock, then sets every slot whose placement on <paramref name="storyId"/> has not
    /// started (<c>StartDate &gt; nowUtc</c>) back to <see cref="SpotlightSlotStatus.Available"/>. A block
    /// that is live at <paramref name="nowUtc"/> counts as started, so its slot stays
    /// <see cref="SpotlightSlotStatus.Redeemed"/> (the build's reading of "has not started" vs "already
    /// aired" — <c>roadmap.md</c> decision row 27 (a)). Idempotent, so a retried execution-strategy
    /// delegate re-runs it safely. Must run inside a transaction the caller has begun.
    /// </summary>
    /// <returns>How many slots went back to their sponsors.</returns>
    public async Task<int> ReleaseUnstartedForStoryAsync(int storyId, DateTime nowUtc)
    {
        // Serialized against RedeemSlotAsync: without the lock a sponsor could book this story after
        // the release and before the caller's delete, and the cascade would then consume that new
        // booking's slot. With it, a racing booking either committed first (and is released here) or
        // waits until the story is gone (and is refused as "Story not found").
        await writeDb.Database.ExecuteSqlRawAsync(SpotlightLocks.BookingLockSql);

        return await writeDb.SpotlightSlots
            .Where(s => s.Status == SpotlightSlotStatus.Redeemed
                        && writeDb.CommunitySpotlights.Any(cs =>
                            cs.SlotId == s.SlotId && cs.StoryId == storyId && cs.StartDate > nowUtc))
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, SpotlightSlotStatus.Available));
    }
}
