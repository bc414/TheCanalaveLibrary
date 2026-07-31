namespace TheCanalaveLibrary.Core;

/// <summary>
/// One search mode's section of the §8.7 override matrix (WU-DiscoveryOverrideUI). Returned by
/// <see cref="IDiscoveryFilterSettingsService.GetMyMatrixAsync"/> — one instance per confirmed-
/// consumer search mode: <c>SearchPage</c>, <c>AutoTreeSearch</c>, <c>AlsoFavorited</c>,
/// <c>AlsoRecommended</c> (the only <see cref="SiteSearchModes"/> constants any code actually
/// passes to <see cref="IDiscoveryDefaultsReadService.GetDefaultExcludedInteractionsAsync"/> as of
/// this WU). Manual <c>TreeSearch</c> and the three <c>Profile*</c> modes are deliberately
/// omitted — they have no consumer, and an editable row that changes nothing is exactly the
/// inert-setting bug class this feature exists to close. If a future WU wires a consumer for one
/// of those modes, add it to <c>ServerDiscoveryFilterSettingsService.ConsumedModes</c> alongside
/// the consumer.
/// </summary>
public record DiscoveryFilterModeDto(
    string SearchModeKey,
    string ModeDisplayName,
    IReadOnlyList<DiscoveryFilterRowDto> Rows);
