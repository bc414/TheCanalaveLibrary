using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// A Community Spotlight <b>placement</b> — one story (plus an optional endorsing recommendation)
/// booked into one discrete calendar block on the homepage (Feature 55, WU-Spotlight; requirements
/// record: <c>audit/Spotlight.md</c>). Created only by redeeming a <see cref="SpotlightSlot"/>
/// entitlement through <c>ISpotlightWriteService.RedeemSlotAsync</c>, which owns all eligibility
/// rules (no self-spotlight, public story, per-story cooldown, block capacity).
///
/// <para>Reshaped 2026-07-11 from the pay-to-feature-era stub: <c>SponsorComment</c> dropped (the
/// attached <see cref="Recommendation"/> *is* the endorsement — an additive composition of
/// existing things, not a new text field) and <c>PaymentId</c> moved to
/// <see cref="SpotlightSlot"/> (payment buys the entitlement, not the placement).</para>
///
/// <para><see cref="StartDate"/>/<see cref="EndDate"/> are the booked block's concrete window
/// (UTC, future-dated at creation). The block grid itself is computed
/// (<see cref="SpotlightBlocks"/>), never stored — no position column, so the position-count and
/// block-duration settings can change without data rewrites.</para>
/// </summary>
public partial class CommunitySpotlight
{
    [Key]
    public int SpotlightId { get; set; }

    /// <summary>The entitlement this placement consumed. Unique (one placement per slot);
    /// <c>Restrict</c> delete.</summary>
    public int SlotId { get; set; }

    public int StoryId { get; set; }

    /// <summary>Null after the sponsor's account deletion (<c>SetNull</c>) — the placement
    /// survives; the story keeps its visibility.</summary>
    public int? SponsoringUserId { get; set; }

    /// <summary>Optional endorsing recommendation shown beside the story — anyone's (the
    /// no-self rule applies to the <em>story</em>, not the recommendation). Null = the display's
    /// blank-rec state; <c>SetNull</c> when the recommendation is deleted.</summary>
    public int? RecommendationId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    /// <summary>Stamped by <c>SpotlightGoLiveWorker</c> when the go-live notifications
    /// (<c>StorySpotlighted</c>/<c>RecommendationSpotlighted</c>) have fired — the fires-once
    /// idempotency marker. Null until the window opens (or forever, if the whole window elapsed
    /// while the app was down — the sweep never notifies after the fact).</summary>
    public DateTime? GoLiveNotifiedUtc { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual SpotlightSlot Slot { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;

    public virtual User? SponsoringUser { get; set; }

    public virtual Recommendation? Recommendation { get; set; }
}
