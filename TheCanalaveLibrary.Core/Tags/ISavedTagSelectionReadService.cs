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

    /// <summary>
    /// Returns one selection for its <b>permalink</b> (<c>/discover/selection/{id}/{*slug}</c>,
    /// decision row 13), or <c>null</c>. Unlike <see cref="GetSelectionDetailAsync"/> this read is
    /// anonymous-callable, so it enforces both gates itself and is deliberately stricter:
    /// <list type="bullet">
    ///   <item>the selection must be <c>IsPublic</c> — a private one is never permalinked, not even
    ///         for its owner (same rule as <see cref="GetPublicSelectionsByUserAsync"/>: a link
    ///         exists to be shared, and an unpublished selection has nothing to share), and</item>
    ///   <item>the owner's <c>ProfileVisibility</c> must admit the caller — Class A access control
    ///         (<c>design/access-gating-first-principles.md</c>), enforced server-side because the
    ///         adversary picks the access path.</item>
    /// </list>
    /// Missing, unpublished and not-visible are all the same contractual <c>null</c>: callers must
    /// never distinguish them, or the permalink becomes an existence oracle for private profiles.
    /// </summary>
    Task<SavedTagSelectionDetailDto?> GetPublicSelectionByIdAsync(int id);
}
