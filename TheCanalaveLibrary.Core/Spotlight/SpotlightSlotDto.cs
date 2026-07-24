namespace TheCanalaveLibrary.Core;

/// <summary>One of the active user's redeemable slot entitlements (redemption page).
/// <paramref name="MaxStoryRating"/>: E = non-M slot, M = Mature-pool slot (WU-AccessGate
/// dedicated pools; defaulted so pre-existing constructions stay valid).</summary>
public record SpotlightSlotDto(
    int SlotId,
    SpotlightSlotSource Source,
    DateTime GrantedUtc,
    Rating MaxStoryRating = Rating.E);
