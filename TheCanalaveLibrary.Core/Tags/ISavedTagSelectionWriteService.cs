namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Saved Tag Selections service contract (Feature 15, WU43). Every method requires
/// an authenticated user (<see cref="IActiveUserContext.UserId"/>); update/delete additionally require
/// ownership. No per-user cap (unlike <c>Vouch</c>'s 5) — deliberate, see <c>audit/Tags.md</c>
/// Feature 15.
/// </summary>
public interface ISavedTagSelectionWriteService
{
    /// <summary>
    /// Creates a new selection owned by the active user (the "Save current…" dialog). Throws
    /// <see cref="SavedTagSelectionValidationException"/> on invalid input, an empty tag set, or a
    /// duplicate nickname for this user.
    /// </summary>
    Task<int> CreateAsync(SavedTagSelectionInput input);

    /// <summary>
    /// Overwrites an existing selection's nickname, description, public flag, and tag entries
    /// (replaces the entry set wholesale — not a merge). Owner-only — throws
    /// <see cref="UnauthorizedAccessException"/> otherwise. Backs all four Load-flyout ⋯ actions
    /// (overwrite tags / rename / edit note / make public-private) — the caller assembles the full
    /// <see cref="SavedTagSelectionInput"/> reflecting the single field it changed plus the rest
    /// unchanged.
    /// </summary>
    Task UpdateAsync(int id, SavedTagSelectionInput input);

    /// <summary>Deletes a selection. Owner-only.</summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// Copy-on-write share: creates a new selection owned by the active user, copying
    /// <paramref name="sourceId"/>'s tag entries, description, and nickname (disambiguated via
    /// <see cref="SavedTagSelectionValidations.DisambiguateCopyNickname"/> if it collides). The copy's
    /// <c>IsPublic</c> is always <c>false</c> regardless of the source's — sharing is not transitive.
    /// Throws <see cref="SavedTagSelectionValidationException"/> if the source doesn't exist, isn't
    /// public, and isn't owned by the caller.
    /// </summary>
    Task<int> CopyPublicSelectionAsync(int sourceId);
}
