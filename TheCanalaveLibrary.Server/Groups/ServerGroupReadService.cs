using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Groups (Features 38/39/40, WU32). All list queries rely on
/// the <c>GroupAudience</c> named query filter — Mature groups are invisible to mature-disabled
/// users without any per-method guard needed. See <c>cross-cutting.md</c>
/// §"Group Audience-Visibility Filter."
/// </summary>
public class ServerGroupReadService(
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser) : IGroupReadService
{
    /// <summary>
    /// Exposed as a protected property so the derived write service can access the user context
    /// without double-capturing the constructor parameter (eliminates CS9107/CS9124 warnings).
    /// See <c>layer2-services.md</c> §"CS9107/CS9124."
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    public async Task<(GroupCardDto[] Items, int TotalCount)> GetListingsAsync(int page, int pageSize)
    {
        // GroupAudience filter is applied automatically — Mature groups filtered for mature-disabled users.
        IQueryable<Group> query = readDb.Groups.OrderByDescending(g => g.DateCreated);

        int totalCount = await query.CountAsync();
        if (totalCount == 0) return ([], 0);

        List<GroupCardDto> items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GroupCardDto(
                g.GroupId,
                g.GroupName,
                g.Description,
                // Derived at projection time from the two rating columns; not stored.
                GroupAudienceTypeMapper.FromRatings(g.AudienceRating, g.MaxContentRating),
                g.GroupMembers.Count,
                g.DateCreated))
            .ToListAsync();

        return (items.ToArray(), totalCount);
    }

    public async Task<GroupDetailDto?> GetByIdAsync(int groupId)
    {
        int? currentUserId = ActiveUser.UserId;

        // Audience filter applied automatically. Returns null if not visible or not found.
        var row = await readDb.Groups
            .Where(g => g.GroupId == groupId)
            .Select(g => new
            {
                g.GroupId,
                g.GroupName,
                g.Description,
                g.AudienceRating,
                g.MaxContentRating,
                g.CreatorId,
                CreatorDisplayName = g.Creator != null ? g.Creator.UserName : null,
                MemberCount = g.GroupMembers.Count,
                g.DateCreated,
                CurrentUserRole = currentUserId != null
                    ? g.GroupMembers
                        .Where(m => m.UserId == currentUserId)
                        .Select(m => (GroupRole?)m.Role)
                        .FirstOrDefault()
                    : (GroupRole?)null,
                StoryIds = g.GroupStories
                    .Select(gs => gs.StoryId)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (row is null) return null;

        // Build the folder tree from a flat list.
        List<GroupFolderDto> folderTree = await BuildFolderTreeAsync(groupId);

        return new GroupDetailDto(
            row.GroupId,
            row.GroupName,
            row.Description,
            GroupAudienceTypeMapper.FromRatings(row.AudienceRating, row.MaxContentRating),
            row.MaxContentRating,
            row.CreatorId,
            row.CreatorDisplayName,
            row.MemberCount,
            row.DateCreated,
            row.CurrentUserRole,
            folderTree,
            row.StoryIds);
    }

    public async Task<GroupRole?> GetCurrentUserRoleAsync(int groupId)
    {
        int? userId = ActiveUser.UserId;
        if (userId is null) return null;

        return await readDb.GroupMembers
            .Where(m => m.GroupId == groupId && m.UserId == userId)
            .Select(m => (GroupRole?)m.Role)
            .FirstOrDefaultAsync();
    }

    public async Task<(GroupMemberDto[] Members, int TotalCount)> GetMembersAsync(
        int groupId, int page, int pageSize)
    {
        IQueryable<GroupMember> query = readDb.GroupMembers
            .Where(m => m.GroupId == groupId)
            .OrderBy(m => m.DateJoined);

        int totalCount = await query.CountAsync();
        if (totalCount == 0) return ([], 0);

        string defaultAvatar = "/img/default-avatar.svg";

        List<GroupMemberDto> members = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new GroupMemberDto(
                m.UserId,
                m.User != null ? m.User.UserName : null,
                m.User != null
                    ? (m.User.ProfilePictureRelativeUrl ?? defaultAvatar)
                    : defaultAvatar,
                m.Role,
                m.DateJoined))
            .ToListAsync();

        return (members.ToArray(), totalCount);
    }

    // ── Internal helper ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loads all folders for a group flat, then assembles the tree in-memory.
    /// EF does not support recursive CTEs in a single LINQ query; loading flat and
    /// re-nesting in-memory is the clean alternative for folder trees of practical depth.
    /// </summary>
    protected async Task<List<GroupFolderDto>> BuildFolderTreeAsync(int groupId)
    {
        // Flat load — includes each folder's direct story assignments.
        var flatFolders = await readDb.GroupFolders
            .Where(f => f.GroupId == groupId)
            .OrderBy(f => f.SortOrder)
            .Select(f => new
            {
                f.GroupFolderId,
                f.GroupId,
                f.ParentFolderId,
                f.Name,
                f.MaxRating,
                f.SortOrder,
                StoryIds = f.GroupStories.Select(gs => gs.StoryId).ToList()
            })
            .ToListAsync();

        // Index by id for fast child lookup.
        var byParent = flatFolders.ToLookup(f => f.ParentFolderId);

        // Recursive builder.
        List<GroupFolderDto> BuildChildren(int? parentId) =>
            byParent[parentId]
                .Select(f => new GroupFolderDto(
                    f.GroupFolderId,
                    f.GroupId,
                    f.ParentFolderId,
                    f.Name,
                    f.MaxRating,
                    f.SortOrder,
                    f.StoryIds,
                    BuildChildren(f.GroupFolderId)))
                .ToList();

        return BuildChildren(null);
    }
}
