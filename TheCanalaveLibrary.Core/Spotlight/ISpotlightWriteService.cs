namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Community Spotlight cluster. One operation: redeem a granted slot into a
/// booked placement. Granting slots is NOT here — that's <see cref="ISpotlightSlotAllocator"/>,
/// the mods-now / donation-pipeline-later seam.
/// </summary>
public interface ISpotlightWriteService : ISpotlightReadService
{
    /// <summary>
    /// Consumes one of the caller's <c>Available</c> slots to book a placement. Validates, inside
    /// one advisory-lock-serialized transaction (two users racing for a block's last opening must
    /// not both succeed): slot ownership + status; story exists, is not the caller's own
    /// (no self-spotlight), is publicly visible (not Draft/PendingApproval/Rejected, not taken
    /// down); optional recommendation belongs to the story, is Approved and not taken down
    /// (anyone's — self-recommendation is allowed); block start on-grid, not fully past, within
    /// the horizon; per-story cooldown; block capacity. Throws
    /// <see cref="SpotlightValidationException"/> on any violation. No notification fires here —
    /// go-live notifications come from the worker when the window opens.
    /// </summary>
    Task RedeemSlotAsync(RedeemSpotlightSlotDto dto);
}
