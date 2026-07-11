namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Saved Tag Selections service contract (Feature 15, WU43). A selection persists
/// only the tag include/exclude axis — see <c>layer2-services.md</c> §"Saved Tag Selections Persist
/// Only the Tag Axis" for what it deliberately excludes and why.
/// </summary>
public interface ISavedTagSelectionReadService
{
    /// <summary>
    /// Returns every selection owned by the active user, in <paramref name="sort"/> order. Anonymous
    /// callers (no <see cref="IActiveUserContext.UserId"/>) get an empty list — saving requires an
    /// account. Backs the <c>SavedTagSelectionLoadFlyout</c> list; its nickname text-filter is applied
    /// client-side over this per-user (typically small) set.
    /// </summary>
    Task<List<SavedTagSelectionSummaryDto>> GetMySelectionsAsync(SavedTagSelectionSortEnum sort);

    /// <summary>
    /// Returns the full hydrated detail for one selection, or <c>null</c> when it doesn't exist, or
    /// exists but is neither owned by the active user nor public. Used by both "Apply" (Load flyout)
    /// and "Add to my filters" (profile tab copy-on-write).
    /// </summary>
    Task<SavedTagSelectionDetailDto?> GetSelectionDetailAsync(int id);

    /// <summary>
    /// Returns every <c>IsPublic</c> selection owned by <paramref name="userId"/>, newest-first. Backs
    /// the profile <c>ProfileTab.TagSelections</c> tab. Never includes private selections, even when
    /// the caller is the profile owner viewing their own page (that view uses
    /// <see cref="GetMySelectionsAsync"/> instead, via the Load flyout).
    /// </summary>
    Task<List<SavedTagSelectionDetailDto>> GetPublicSelectionsByUserAsync(int userId);
}
