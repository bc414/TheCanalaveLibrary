namespace TheCanalaveLibrary.Core;

/// <summary>
/// One togglable row in the §8.7 per-search-mode override matrix (WU-DiscoveryOverrideUI, closes
/// tracker item B7). <see cref="InteractionType"/> mirrors the mapping
/// <see cref="ServerDiscoveryDefaultsReadService"/>'s internal <c>KeyToEnum</c> table already
/// performs — the same six kinds <c>UserStoryInteractionFilter</c>'s checkboxes expose, so the
/// settings page and the live filter panel can share display wording.
/// </summary>
/// <param name="FilterKey">
/// The catalog key (<see cref="UserStoryInteractionFilters"/>) this row edits.
/// </param>
/// <param name="InteractionType">The mapped enum value, for shared label lookup.</param>
/// <param name="SystemDefault">The seeded <c>DefaultUserStoryInteractionFilterSetting</c> value.</param>
/// <param name="EffectiveValue">
/// The value actually applied to this mode's queries today: the user's override when one exists,
/// otherwise <paramref name="SystemDefault"/>.
/// </param>
/// <param name="IsOverridden">
/// <c>true</c> when a <see cref="UserStoryInteractionFilterSetting"/> row exists for this
/// (user × mode × key). <c>false</c> means the row is absent and <see cref="EffectiveValue"/> is
/// just the system default — the sparse-override contract
/// <see cref="IDiscoveryFilterSettingsService.SetOverrideAsync"/> maintains.
/// </param>
public record DiscoveryFilterRowDto(
    string FilterKey,
    UserStoryInteractionTypeEnum InteractionType,
    bool SystemDefault,
    bool EffectiveValue,
    bool IsOverridden);
