namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Groups service contract. Inherits the read interface (CQRS-lite).
/// <para>
/// <b>Membership:</b> open join, permanent — no kicking (settled WU32).
/// <b>Roles:</b> Member and Admin only. Creator is auto-added as Admin on group creation.
/// Admin-gated methods throw <see cref="UnauthorizedAccessException"/> for non-admins.
/// </para>
/// <para>
/// <b>Content-rating waterfall</b> on <see cref="AddStoryAsync"/> — see
/// <c>layer2-services.md</c> §"Group Rating Waterfall".
/// </para>
/// </summary>
public interface IGroupWriteService : IGroupReadService
{
    // ── Group CRUD ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new group. The caller is automatically added as an Admin member.
    /// <c>CreatorId</c> is server-stamped from <see cref="IActiveUserContext.UserId"/>.
    /// Sanitizes <c>dto.Description</c> before persisting.
    /// </summary>
    /// <returns>The new <c>GroupId</c>.</returns>
    /// <exception cref="GroupValidationException">Name/description validation fails.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<int> CreateGroupAsync(CreateGroupDto dto);

    /// <summary>
    /// Updates an existing group's name, description, and audience type. Admin-only.
    /// Sanitizes <c>dto.Description</c> before persisting.
    /// </summary>
    /// <exception cref="GroupValidationException">Validation fails.</exception>
    /// <exception cref="KeyNotFoundException">Group not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not an Admin of this group.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task UpdateGroupAsync(UpdateGroupDto dto);

    // ── Membership ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adds the current user to the group as a Member. Idempotent — no-op when already a member.
    /// Only callable if the group is visible to the current user (audience filter).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Group not found (or not visible to current user).</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task JoinAsync(int groupId);

    /// <summary>
    /// Removes the current user from the group. Idempotent — no-op when not a member.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Group not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task LeaveAsync(int groupId);

    // ── Story management ──────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a story to the group (member action). Enforces the content-rating waterfall:
    /// tier 2 (<c>story.Rating &gt; group.MaxContentRating</c>) and optionally tier 3
    /// (<c>story.Rating &gt; folder.MaxRating</c>) when <c>dto.GroupFolderId</c> is set.
    /// </summary>
    /// <exception cref="ContentRatingExceededException">Story rating exceeds group or folder ceiling.</exception>
    /// <exception cref="KeyNotFoundException">Group, story, or folder not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not a member of this group.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task AddStoryAsync(AddGroupStoryDto dto);

    /// <summary>
    /// Removes a <see cref="GroupStory"/> from the group. Admin-only.
    /// Also removes any folder assignments for the story.
    /// </summary>
    /// <exception cref="KeyNotFoundException">GroupStory not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not an Admin of this group.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task RemoveStoryAsync(int groupStoryId);

    /// <summary>
    /// Assigns an existing <see cref="GroupStory"/> to a folder within the same group (admin).
    /// Validates <c>folder.MaxRating ≥ story.Rating</c>.
    /// </summary>
    /// <exception cref="ContentRatingExceededException">Story rating exceeds folder ceiling.</exception>
    /// <exception cref="KeyNotFoundException">GroupStory or folder not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not an Admin of this group.</exception>
    Task AssignStoryToFolderAsync(int groupStoryId, int groupFolderId);

    /// <summary>Removes a <see cref="GroupStory"/> from a folder (admin). Does not remove the story from the group.</summary>
    /// <exception cref="KeyNotFoundException">GroupStory or folder not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not an Admin of this group.</exception>
    Task UnassignStoryFromFolderAsync(int groupStoryId, int groupFolderId);

    // ── Folder management (admin-only) ────────────────────────────────────────────

    /// <summary>
    /// Creates a new folder in the group. Admin-only. <c>dto.MaxRating</c> must be ≤ the group's
    /// <see cref="Group.MaxContentRating"/>. Folder names must be unique within their parent.
    /// </summary>
    /// <exception cref="GroupValidationException">Name taken or MaxRating exceeds group cap.</exception>
    /// <exception cref="KeyNotFoundException">Group not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not an Admin.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<int> CreateFolderAsync(CreateFolderDto dto);

    /// <summary>Renames a folder (admin). Name must be unique within parent.</summary>
    /// <exception cref="GroupValidationException">Name taken.</exception>
    /// <exception cref="KeyNotFoundException">Folder not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not an Admin.</exception>
    Task RenameFolderAsync(int groupFolderId, string newName);

    /// <summary>Hard-deletes a folder (admin). Stories remain in the group; only folder assignments CASCADE.</summary>
    /// <exception cref="KeyNotFoundException">Folder not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not an Admin.</exception>
    Task DeleteFolderAsync(int groupFolderId);

    /// <summary>Updates a folder's <see cref="GroupFolder.SortOrder"/> (admin).</summary>
    Task ReorderFolderAsync(int groupFolderId, int newSortOrder);
}
