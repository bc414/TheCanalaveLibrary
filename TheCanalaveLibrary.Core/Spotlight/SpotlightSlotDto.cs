namespace TheCanalaveLibrary.Core;

/// <summary>One of the active user's redeemable slot entitlements (redemption page).</summary>
public record SpotlightSlotDto(
    int SlotId,
    SpotlightSlotSource Source,
    DateTime GrantedUtc);
