namespace TheCanalaveLibrary.Core;

/// <summary>
/// Full display DTO for a single <see cref="SavedTagSelection"/> (WU43) — hydrated tag chips for
/// <c>TagFilter.ApplySavedSelectionAsync</c> and for the profile Tag Selections tab's public cards.
/// Chips carry the raw <see cref="TagChipDto.SpriteIdentifier"/>; sprites resolve at render time (see
/// <c>layer2-services.md</c> "Sprite URLs Are Resolved At Render Time").
/// </summary>
public record SavedTagSelectionDetailDto(
    int Id,
    string Nickname,
    string? Description,
    bool IsPublic,
    int OwnerUserId,
    IReadOnlyList<TagChipDto> IncludedTags,
    IReadOnlyList<TagChipDto> ExcludedTags);
