namespace TheCanalaveLibrary.Core;

/// <summary>
/// Redemption request: consume <see cref="SlotId"/> to book <see cref="StoryId"/> (someone
/// else's story — service-enforced) into the block starting at <see cref="BlockStartUtc"/>
/// (must lie on the <see cref="SpotlightBlocks"/> grid), optionally displaying
/// <see cref="RecommendationId"/> beside it. All eligibility rules are validated
/// server-side in <see cref="ISpotlightWriteService.RedeemSlotAsync"/> — UI affordances are
/// not gates.
/// </summary>
public record RedeemSpotlightSlotDto(
    int SlotId,
    int StoryId,
    int? RecommendationId,
    DateTime BlockStartUtc);
