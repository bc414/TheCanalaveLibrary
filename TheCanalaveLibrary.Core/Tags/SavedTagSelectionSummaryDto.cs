namespace TheCanalaveLibrary.Core;

/// <summary>
/// Lightweight row for the <c>SavedTagSelectionLoadFlyout</c> list (WU43) — one per selection owned
/// by the active user. <see cref="IncludedCount"/>/<see cref="ExcludedCount"/> are cheap raw counts for
/// display; the full tag chips are fetched only on Apply via
/// <see cref="ISavedTagSelectionReadService.GetSelectionDetailAsync"/>.
/// </summary>
public record SavedTagSelectionSummaryDto(
    int Id,
    string Nickname,
    string? Description,
    bool IsPublic,
    DateTime DateCreated,
    int IncludedCount,
    int ExcludedCount);
