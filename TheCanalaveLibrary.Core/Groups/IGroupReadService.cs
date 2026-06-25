namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Groups service contract (Features 38/39/40, WU32).
/// All queries respect the <c>GroupAudience</c> named query filter — Mature groups are invisible
/// to users with mature content disabled. See <c>cross-cutting.md</c> "Group Audience-Visibility Filter."
/// </summary>
public interface IGroupReadService
{
    /// <summary>
    /// Returns a page of groups visible to the current user (audience filter applied), ordered
    /// newest-first by <see cref="Group.DateCreated"/>.
    /// </summary>
    Task<(GroupCardDto[] Items, int TotalCount)> GetListingsAsync(int page, int pageSize);

    /// <summary>
    /// Returns the full detail DTO for a single group, or <c>null</c> when the group does not
    /// exist or is not visible to the current user (audience filter). The <c>GroupAudience</c>
    /// filter is applied via <c>IgnoreQueryFilters</c> opt-out only on admin/creator paths.
    /// </summary>
    Task<GroupDetailDto?> GetByIdAsync(int groupId);

    /// <summary>
    /// Returns the <see cref="GroupRole"/> of the current user within the specified group, or
    /// <c>null</c> when the user is not a member (anonymous or non-member).
    /// </summary>
    Task<GroupRole?> GetCurrentUserRoleAsync(int groupId);

    /// <summary>
    /// Returns a page of members in the specified group, ordered by join date ascending.
    /// </summary>
    Task<(GroupMemberDto[] Members, int TotalCount)> GetMembersAsync(int groupId, int page, int pageSize);
}
