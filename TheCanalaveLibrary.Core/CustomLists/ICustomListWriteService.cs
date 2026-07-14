namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Custom Lists service contract (Feature 51, WU-CustomLists). Every method
/// requires an authenticated user; mutations of an existing list additionally require ownership
/// (<see cref="UnauthorizedAccessException"/> otherwise). Domain failures throw
/// <see cref="CustomListValidationException"/>. Adds are silent by design — no notification is sent
/// to a story's author when their story is filed into a personal list (deliberately unlike
/// <c>GroupStory</c> adds; part of the feature's private-shelf appeal). No rate limiting — creates
/// are bounded by <see cref="CustomListValidations.MaxListsPerUser"/>, matching Saved Tag
/// Selections (the structural precedent).
/// </summary>
public interface ICustomListWriteService : ICustomListReadService
{
    /// <summary>
    /// Creates a list owned by the active user. Name is required, unique per user
    /// (case-insensitive), max <see cref="CustomListValidations.MaxListNameLength"/> chars; the
    /// <see cref="CustomListValidations.MaxListsPerUser"/> cap applies. Returns the new list id.
    /// </summary>
    Task<int> CreateListAsync(string listName, bool isPublic);

    /// <summary>Renames a list. Owner-only; same name rules as create (cap not re-checked).</summary>
    Task RenameListAsync(int listId, string newListName);

    /// <summary>Sets a list public/private. Owner-only. Idempotent.</summary>
    Task SetListVisibilityAsync(int listId, bool isPublic);

    /// <summary>Deletes a list (entries cascade). Owner-only.</summary>
    Task DeleteListAsync(int listId);

    /// <summary>
    /// Adds a story to a list the active user owns. Idempotent — a story already in the list is a
    /// no-op (composite PK <c>(ListId, StoryId)</c>). Throws <see cref="KeyNotFoundException"/>
    /// when the story doesn't exist.
    /// </summary>
    Task AddStoryAsync(int listId, int storyId);

    /// <summary>Removes a story from a list the active user owns. Idempotent.</summary>
    Task RemoveStoryAsync(int listId, int storyId);

    /// <summary>
    /// View + optional clone (the settled sharing model): creates a new list owned by the active
    /// user, copying the source's entries. The copy contains <b>only entries whose story is visible
    /// to the cloner</b> (content-rating/takedown filters — never smuggles hidden content into
    /// their account); starts <c>IsPublic=false</c> regardless of the source's (sharing is not
    /// transitive — same rule as <c>CopyPublicSelectionAsync</c>); name disambiguated via
    /// <see cref="CustomListValidations.DisambiguateCloneName"/> on collision; entry
    /// <c>DateAdded</c> stamps are the clone time, not the source's. Self-cloning is allowed.
    /// Throws <see cref="CustomListValidationException"/> when the source doesn't exist or is
    /// neither public nor owned by the caller, or when the cloner is at the list cap.
    /// </summary>
    Task<int> CloneListAsync(int sourceListId);
}
