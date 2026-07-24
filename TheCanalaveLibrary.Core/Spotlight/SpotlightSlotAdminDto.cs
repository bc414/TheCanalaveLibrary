namespace TheCanalaveLibrary.Core;

/// <summary>One grant row on the mod surface (<c>/mod/spotlight</c>) — who holds it, its
/// lifecycle state, and who granted it.</summary>
public record SpotlightSlotAdminDto(
    int SlotId,
    int? GrantedToUserId,
    string? GrantedToUserName,
    SpotlightSlotSource Source,
    SpotlightSlotStatus Status,
    DateTime GrantedUtc,
    Rating MaxStoryRating = Rating.E);
