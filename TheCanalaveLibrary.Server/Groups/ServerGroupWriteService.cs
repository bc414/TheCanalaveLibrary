using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Groups (WU32). Inherits the read path via primary-constructor
/// chaining. Enforces the three-tier content-rating waterfall on story adds (see
/// <c>layer2-services.md</c> §"Group Rating Waterfall"). Admin-gated methods throw
/// <see cref="UnauthorizedAccessException"/> for non-admins. Sanitizes
/// <see cref="Group.Description"/> once on save.
/// </summary>
public class ServerGroupWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer,
    INotificationWriteService notifications,
    IWriteRateLimitService rateLimit,
    ILogger<ServerGroupWriteService> logger)
    : ServerGroupReadService(readDbFactory, activeUser), IGroupWriteService
{
    // ── Group CRUD ────────────────────────────────────────────────────────────────

    public async Task<int> CreateGroupAsync(CreateGroupDto dto)
    {
        if (ActiveUser.UserId is not int creatorId)
            throw new InvalidOperationException("Creating a group requires an authenticated user.");

        // Group creation makes content every user sees on /groups — the exact "abuse-prone
        // create" shape security.md's throttle rule covers (MA-508, 2026-07-18).
        rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, creatorId);

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new GroupValidationException(errors);

        (Rating audienceRating, Rating maxContentRating) = GroupAudienceTypeMapper.ToRatings(dto.AudienceType);
        string? sanitizedDesc = dto.Description is not null
            ? sanitizer.Sanitize(dto.Description)
            : null;

        Group group = new()
        {
            CreatorId       = creatorId,
            GroupName       = dto.GroupName.Trim(),
            Description     = sanitizedDesc,
            AudienceRating  = audienceRating,
            MaxContentRating = maxContentRating,
            DateCreated     = DateTime.UtcNow
        };

        writeDb.Groups.Add(group);
        await writeDb.SaveChangesAsync();

        // Creator is automatically the first Admin member.
        writeDb.GroupMembers.Add(new GroupMember
        {
            GroupId    = group.GroupId,
            UserId     = creatorId,
            Role       = GroupRole.Admin,
            DateJoined = DateTime.UtcNow
        });
        await writeDb.SaveChangesAsync();

        return group.GroupId;
    }

    public async Task UpdateGroupAsync(UpdateGroupDto dto)
    {
        int userId = RequireAuthenticatedUser();
        await RequireAdminAsync(dto.GroupId, userId);

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new GroupValidationException(errors);

        Group? group = await writeDb.Groups.FirstOrDefaultAsync(g => g.GroupId == dto.GroupId);
        if (group is null) throw new KeyNotFoundException($"Group {dto.GroupId} not found.");

        (Rating audienceRating, Rating maxContentRating) = GroupAudienceTypeMapper.ToRatings(dto.AudienceType);

        group.GroupName       = dto.GroupName.Trim();
        group.Description     = dto.Description is not null ? sanitizer.Sanitize(dto.Description) : null;
        group.AudienceRating  = audienceRating;
        group.MaxContentRating = maxContentRating;

        await writeDb.SaveChangesAsync();
    }

    // ── Membership ────────────────────────────────────────────────────────────────

    public async Task JoinAsync(int groupId)
    {
        int userId = RequireAuthenticatedUser();

        // Audience filter is active on writeDb too — if the group is invisible to this user, the
        // AnyAsync returns false and we throw KeyNotFoundException (correct: can't join what you can't see).
        bool groupExists = await writeDb.Groups.AnyAsync(g => g.GroupId == groupId);
        if (!groupExists) throw new KeyNotFoundException($"Group {groupId} not found.");

        // Idempotent — no-op if already a member.
        bool alreadyMember = await writeDb.GroupMembers
            .AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (alreadyMember) return;

        writeDb.GroupMembers.Add(new GroupMember
        {
            GroupId    = groupId,
            UserId     = userId,
            Role       = GroupRole.Member,
            DateJoined = DateTime.UtcNow
        });
        await writeDb.SaveChangesAsync();

        // Increment GroupsJoined counter (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.GroupsJoined, us => us.GroupsJoined + 1));
    }

    public async Task LeaveAsync(int groupId)
    {
        int userId = RequireAuthenticatedUser();

        // Idempotent — no-op if not a member.
        GroupMember? member = await writeDb.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (member is null) return;

        writeDb.GroupMembers.Remove(member);
        await writeDb.SaveChangesAsync();

        // Decrement GroupsJoined counter (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.GroupsJoined, us => us.GroupsJoined - 1));
    }

    // ── Story management ──────────────────────────────────────────────────────────

    public async Task AddStoryAsync(AddGroupStoryDto dto)
    {
        int userId = RequireAuthenticatedUser();
        await RequireMemberAsync(dto.GroupId, userId);

        // Write context is unfiltered — loads the group and story regardless of audience rating or
        // content rating, so a member can always act on their group and any story. The tier-2 check
        // below enforces the group's MaxContentRating ceiling on the loaded story.
        Group? group = await writeDb.Groups
            .FirstOrDefaultAsync(g => g.GroupId == dto.GroupId);
        if (group is null) throw new KeyNotFoundException($"Group {dto.GroupId} not found.");

        Story? story = await writeDb.Stories
            .FirstOrDefaultAsync(s => s.StoryId == dto.StoryId);
        if (story is null) throw new KeyNotFoundException($"Story {dto.StoryId} not found.");

        // Tier 2: group MaxContentRating ceiling.
        if (story.Rating > group.MaxContentRating)
            throw new ContentRatingExceededException(
                $"Story rating {story.Rating} exceeds this group's maximum content rating {group.MaxContentRating}.");

        // Check for duplicate before inserting.
        bool alreadyAdded = await writeDb.GroupStories
            .AnyAsync(gs => gs.GroupId == dto.GroupId && gs.StoryId == dto.StoryId);
        if (!alreadyAdded)
        {
            GroupStory groupStory = new()
            {
                GroupId      = dto.GroupId,
                StoryId      = dto.StoryId,
                AddedByUserId = userId,
                DateAdded    = DateTime.UtcNow
            };
            writeDb.GroupStories.Add(groupStory);
            await writeDb.SaveChangesAsync();

            // Tier 3: optional folder assignment.
            if (dto.GroupFolderId.HasValue)
            {
                await AssignStoryToFolderInternalAsync(groupStory.GroupStoryId, dto.GroupFolderId.Value, story.Rating);
            }
        }

        // Notifications: fan-out to members + author-added notification (best-effort post-commit).
        int? storyAuthorId = story.AuthorId;
        if (storyAuthorId.HasValue)
        {
            try
            {
                await notifications.NotifyNewGroupStoryAsync(dto.GroupId, storyAuthorId.Value, userId);
            }
            catch (Exception ex)
            {
                // Best-effort — notification failure must not roll back the primary action.
                logger.LogWarning(ex,
                    "NewGroupStory notification failed for story {StoryId} added to group {GroupId}",
                    dto.StoryId, dto.GroupId);
            }
        }
    }

    public async Task RemoveStoryAsync(int groupStoryId)
    {
        int userId = RequireAuthenticatedUser();

        GroupStory? gs = await writeDb.GroupStories
            .Include(gs => gs.GroupFolders)
            .FirstOrDefaultAsync(gs => gs.GroupStoryId == groupStoryId);
        if (gs is null) throw new KeyNotFoundException($"GroupStory {groupStoryId} not found.");

        await RequireAdminAsync(gs.GroupId, userId);

        // Folder assignments cascade from GroupStory deletion (many-to-many join cleared by EF).
        writeDb.GroupStories.Remove(gs);
        await writeDb.SaveChangesAsync();
    }

    public async Task AssignStoryToFolderAsync(int groupStoryId, int groupFolderId)
    {
        int userId = RequireAuthenticatedUser();

        GroupStory? gs = await writeDb.GroupStories
            .Include(gs => gs.Story)
            .FirstOrDefaultAsync(gs => gs.GroupStoryId == groupStoryId);
        if (gs is null) throw new KeyNotFoundException($"GroupStory {groupStoryId} not found.");

        await RequireAdminAsync(gs.GroupId, userId);

        Rating storyRating = gs.Story?.Rating ?? Rating.E;
        await AssignStoryToFolderInternalAsync(groupStoryId, groupFolderId, storyRating);
    }

    public async Task UnassignStoryFromFolderAsync(int groupStoryId, int groupFolderId)
    {
        int userId = RequireAuthenticatedUser();

        GroupStory? gs = await writeDb.GroupStories
            .Include(gs => gs.GroupFolders.Where(f => f.GroupFolderId == groupFolderId))
            .FirstOrDefaultAsync(gs => gs.GroupStoryId == groupStoryId);
        if (gs is null) throw new KeyNotFoundException($"GroupStory {groupStoryId} not found.");

        await RequireAdminAsync(gs.GroupId, userId);

        GroupFolder? folder = gs.GroupFolders.FirstOrDefault();
        if (folder is not null)
            gs.GroupFolders.Remove(folder);

        await writeDb.SaveChangesAsync();
    }

    // ── Folder management ─────────────────────────────────────────────────────────

    public async Task<int> CreateFolderAsync(CreateFolderDto dto)
    {
        int userId = RequireAuthenticatedUser();
        await RequireAdminAsync(dto.GroupId, userId);

        // Validate MaxRating ≤ group cap.
        Rating groupCap = await writeDb.Groups
            .Where(g => g.GroupId == dto.GroupId)
            .Select(g => g.MaxContentRating)
            .FirstOrDefaultAsync();

        if (dto.MaxRating > groupCap)
            throw new GroupValidationException(
                [$"Folder MaxRating ({dto.MaxRating}) cannot exceed the group's MaxContentRating ({groupCap})."]);

        GroupFolder folder = new()
        {
            GroupId        = dto.GroupId,
            ParentFolderId = dto.ParentFolderId,
            Name           = dto.Name.Trim(),
            MaxRating      = dto.MaxRating,
            SortOrder      = dto.SortOrder
        };

        writeDb.GroupFolders.Add(folder);
        await writeDb.SaveChangesAsync();
        return folder.GroupFolderId;
    }

    public async Task RenameFolderAsync(int groupFolderId, string newName)
    {
        int userId = RequireAuthenticatedUser();

        GroupFolder? folder = await writeDb.GroupFolders
            .FirstOrDefaultAsync(f => f.GroupFolderId == groupFolderId);
        if (folder is null) throw new KeyNotFoundException($"Folder {groupFolderId} not found.");

        await RequireAdminAsync(folder.GroupId, userId);

        folder.Name = newName.Trim();
        await writeDb.SaveChangesAsync();
    }

    public async Task DeleteFolderAsync(int groupFolderId)
    {
        int userId = RequireAuthenticatedUser();

        GroupFolder? folder = await writeDb.GroupFolders
            .FirstOrDefaultAsync(f => f.GroupFolderId == groupFolderId);
        if (folder is null) throw new KeyNotFoundException($"Folder {groupFolderId} not found.");

        await RequireAdminAsync(folder.GroupId, userId);

        // GroupStory-GroupFolder many-to-many join rows cascade on folder delete (EF removes them).
        // Child folders: ParentFolderId is SET NULL (configured in GroupFolderConfiguration) —
        // children become root-level folders rather than being orphaned.
        writeDb.GroupFolders.Remove(folder);
        await writeDb.SaveChangesAsync();
    }

    public async Task ReorderFolderAsync(int groupFolderId, int newSortOrder)
    {
        int userId = RequireAuthenticatedUser();

        GroupFolder? folder = await writeDb.GroupFolders
            .FirstOrDefaultAsync(f => f.GroupFolderId == groupFolderId);
        if (folder is null) throw new KeyNotFoundException($"Folder {groupFolderId} not found.");

        await RequireAdminAsync(folder.GroupId, userId);

        folder.SortOrder = newSortOrder;
        await writeDb.SaveChangesAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private int RequireAuthenticatedUser()
    {
        if (ActiveUser.UserId is not int id)
            throw new InvalidOperationException("This operation requires an authenticated user.");
        return id;
    }

    private async Task RequireMemberAsync(int groupId, int userId)
    {
        bool isMember = await writeDb.GroupMembers
            .AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (!isMember)
            throw new UnauthorizedAccessException("You must be a member of this group.");
    }

    private async Task RequireAdminAsync(int groupId, int userId)
    {
        GroupRole? role = await writeDb.GroupMembers
            .Where(m => m.GroupId == groupId && m.UserId == userId)
            .Select(m => (GroupRole?)m.Role)
            .FirstOrDefaultAsync();

        if (role is not GroupRole.Admin)
            throw new UnauthorizedAccessException("You must be an Admin of this group.");
    }

    private async Task AssignStoryToFolderInternalAsync(int groupStoryId, int groupFolderId, Rating storyRating)
    {
        GroupFolder? folder = await writeDb.GroupFolders
            .FirstOrDefaultAsync(f => f.GroupFolderId == groupFolderId);
        if (folder is null) throw new KeyNotFoundException($"Folder {groupFolderId} not found.");

        // Tier 3: folder MaxRating ceiling.
        if (storyRating > folder.MaxRating)
            throw new ContentRatingExceededException(
                $"Story rating {storyRating} exceeds folder MaxRating {folder.MaxRating}.");

        // Load the GroupStory with its current folder collection for the many-to-many add.
        GroupStory? gs = await writeDb.GroupStories
            .Include(gs => gs.GroupFolders)
            .FirstOrDefaultAsync(gs => gs.GroupStoryId == groupStoryId);
        if (gs is null) throw new KeyNotFoundException($"GroupStory {groupStoryId} not found.");

        // Idempotent — no-op if already in this folder.
        if (gs.GroupFolders.All(f => f.GroupFolderId != groupFolderId))
        {
            gs.GroupFolders.Add(folder);
            await writeDb.SaveChangesAsync();
        }
    }
}
