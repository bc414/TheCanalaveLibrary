namespace TheCanalaveLibrary.Core;

/// <summary>
/// How a <see cref="SpotlightSlot"/> entitlement was granted (magic enum — tiny, stable,
/// app-coupled; stored as <c>smallint</c> via <c>HasConversion&lt;short&gt;()</c>).
/// <para><see cref="Donation"/> is the reserved seam for the deferred donation/payment pipeline
/// (see <c>audit/Spotlight.md</c> "Deferred") — no grant path produces it yet;
/// <c>ServerSpotlightSlotAllocator</c> throws for it until that pipeline lands.</para>
/// </summary>
public enum SpotlightSlotSource : short
{
    /// <summary>A moderator awarded the slot from <c>/mod/spotlight</c>.</summary>
    ModAward = 0,

    /// <summary>Reserved: the slot was earned by a site donation (deferred past beta).</summary>
    Donation = 1
}

/// <summary>
/// Lifecycle of a <see cref="SpotlightSlot"/> entitlement. Grants do not expire
/// (settled 2026-07-11 — redemption deadlines are deferred); <see cref="Revoked"/> is the
/// moderator escape hatch for a grant that should no longer be redeemable.
/// </summary>
public enum SpotlightSlotStatus : short
{
    /// <summary>Granted, not yet redeemed — the holder can book a placement with it.</summary>
    Available = 0,

    /// <summary>Consumed by a <see cref="CommunitySpotlight"/> placement (terminal).</summary>
    Redeemed = 1,

    /// <summary>Withdrawn by a moderator before redemption (terminal).</summary>
    Revoked = 2
}
