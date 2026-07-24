using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// A Community Spotlight <b>entitlement</b> — the granted right to book one homepage placement
/// (Feature 55, WU-Spotlight; requirements record: <c>audit/Spotlight.md</c>). Deliberately split
/// from <see cref="CommunitySpotlight"/> (the <b>placement</b>): the donation era changes only how
/// slots are granted (<see cref="SpotlightSlotSource.Donation"/> + <see cref="PaymentId"/>),
/// never what a placement is. Granted via <c>ISpotlightSlotAllocator</c> — the mod-grant /
/// future-payment-pipeline seam.
/// </summary>
public class SpotlightSlot
{
    [Key]
    public int SlotId { get; set; }

    /// <summary>The user entitled to redeem this slot. Null only after account deletion
    /// (<c>SetNull</c> — an orphaned unredeemed slot is dead weight, kept for the audit trail).</summary>
    public int? GrantedToUserId { get; set; }

    /// <summary>The moderator who granted it (<see cref="SpotlightSlotSource.ModAward"/> only;
    /// null for future donation-sourced grants and after mod account deletion).</summary>
    public int? GrantedByUserId { get; set; }

    public SpotlightSlotSource Source { get; set; }

    public SpotlightSlotStatus Status { get; set; }

    /// <summary>
    /// The slot's rating class (WU-AccessGate, settled 2026-07-19): dedicated M and non-M pools.
    /// <see cref="Rating.E"/> (default) = non-M pool (E/T stories only); <see cref="Rating.M"/> =
    /// Mature pool (M stories only). Enforced at redemption — slot-inventory integrity, so an
    /// awardee always knows which audience their slot reaches (mature-off/anon homepage viewers
    /// see only non-M placements; the display-time filtered join already does that).
    /// </summary>
    public Rating MaxStoryRating { get; set; }

    /// <summary>Reserved for the deferred donation pipeline — the payment-provider transaction
    /// reference that earned this slot. Always null for <see cref="SpotlightSlotSource.ModAward"/>.</summary>
    [MaxLength(2048)]
    public string? PaymentId { get; set; }

    public DateTime GrantedUtc { get; set; }

    public virtual User? GrantedToUser { get; set; }

    public virtual User? GrantedByUser { get; set; }

    /// <summary>The placement this slot was redeemed into (null while <see cref="Status"/> is
    /// <see cref="SpotlightSlotStatus.Available"/> or <see cref="SpotlightSlotStatus.Revoked"/>).</summary>
    public virtual CommunitySpotlight? Placement { get; set; }
}
