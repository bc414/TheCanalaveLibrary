using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
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
    // Database names the race backstops match on (GroupConfigurations.cs pins each one), so any other
    // 23505/23503 stays a genuine fault. layer2-services.md §"Group Folder Integrity".
    private const string GroupStoryIndex = "ix_group_stories_group_id_story_id";
    private const string FolderNameIndex = "ix_group_folders_group_id_parent_folder_id_name";
    private const string FolderParentForeignKey = "fk_group_folders_group_folders_parent_folder_id";

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

        // Creating a group counts as joining it (owner ruling D24): the creator's row IS a
        // group_members row, and the recompute (COUNT(*) FROM group_members) counts it. Without this
        // +1, create-then-leave drove GroupsJoined to −1. Post-commit, after the member row saves
        // (D22; layer2-services.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == creatorId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.GroupsJoined, us => us.GroupsJoined + 1));

        return group.GroupId;
    }

    public async Task UpdateGroupAsync(UpdateGroupDto dto)
    {
        int userId = ActiveUser.RequireUserId();
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
        int userId = ActiveUser.RequireUserId();

        // CORRECTION (WU-ParentVisibility): this check used to carry a comment claiming "the audience
        // filter is active on writeDb too." It is not — GroupAudience is declared on
        // ReadOnlyApplicationDbContext only, as this same file states correctly twice below. The false
        // comment described a control that did not exist, and it was load-bearing: a user with mature
        // content off could join an M-audience group, which then unlocked the membership-gated writes
        // (CreateGroupBlogPostAsync, AddStoryAsync) and enrolled them in NewGroupStory/NewGroupBlogPost
        // fan-out for mature content they had explicitly opted out of.
        bool groupExists = await writeDb.Groups.AnyAsync(g => g.GroupId == groupId);
        if (!groupExists) throw new KeyNotFoundException($"Group {groupId} not found.");

        // Kind (g): you cannot join what you cannot see — now actually enforced.
        await using (ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync())
        {
            if (!await GroupVisibilityGuard.IsGroupVisibleAsync(readDb, ActiveUser, groupId))
                throw new KeyNotFoundException($"Group {groupId} not found.");
        }

        // Idempotent — no-op if already a member. One declarative upsert (owner ruling D23): the old
        // AnyAsync-then-Add let two concurrent joins both pass the check, and the loser's insert hit
        // pk_group_members as a raw 500. The column values come from a C#-built entity so its
        // initializers stay the single source of defaults; the columns have no DB default except
        // date_joined, so all of them are listed (layer2-services.md §"Check-then-act posture").
        GroupMember member = new()
        {
            GroupId    = groupId,
            UserId     = userId,
            Role       = GroupRole.Member,
            DateJoined = DateTime.UtcNow
        };
        int inserted = await writeDb.Database.ExecuteSqlAsync($"""
            INSERT INTO group_members (user_id, group_id, role, notify_for_new_story, notify_for_new_blog_post, date_joined)
            VALUES ({member.UserId}, {member.GroupId}, {(short)member.Role}, {member.NotifyForNewStory}, {member.NotifyForNewBlogPost}, {member.DateJoined})
            ON CONFLICT (user_id, group_id) DO NOTHING
            """);

        // The counter moves only for the insert that landed a row, so a concurrent double join
        // counts once (layer2-services.md §"UserStats Updates").
        if (inserted == 0) return;
        await writeDb.UserStats.Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.GroupsJoined, us => us.GroupsJoined + 1));
    }

    public async Task LeaveAsync(int groupId)
    {
        int userId = ActiveUser.RequireUserId();

        // Idempotent — no-op if not a member.
        GroupMember? member = await writeDb.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (member is null) return;

        writeDb.GroupMembers.Remove(member);
        await writeDb.SaveChangesAsync();

        // Decrement GroupsJoined counter (layer2-services.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.GroupsJoined, us => us.GroupsJoined - 1));
    }

    // ── Story management ──────────────────────────────────────────────────────────

    public async Task AddStoryAsync(AddGroupStoryDto dto)
    {
        int userId = ActiveUser.RequireUserId();
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

        // Kind (g), CONFIDENTIALITY axis only. The rating axis here belongs to the tier-2
        // MaxContentRating waterfall below, which is group policy rather than a viewer check and
        // reports its own ContentRatingExceededException (AddStory_Tier2_StoryRatingExceedsGroupMax_
        // Throws asserts that specific type — a viewer-ceiling guard would pre-empt it with a
        // not-found and lose the diagnostic). What the waterfall never asked is whether the story is
        // published at all: a Draft, PendingApproval, Rejected or taken-down story could be filed
        // into a group and its author notified via NotifyNewGroupStoryAsync.
        await using (ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync())
        {
            if (!await StoryVisibilityGuard.IsStoryPublishedAsync(readDb, ActiveUser, dto.StoryId))
                throw new KeyNotFoundException($"Story {dto.StoryId} not found.");
        }

        // Tier 2: group MaxContentRating ceiling.
        if (story.Rating > group.MaxContentRating)
            throw new ContentRatingExceededException(
                $"Story rating {story.Rating} exceeds this group's maximum content rating {group.MaxContentRating}.");

        // A group's stories are a set (owner ruling D23). This check is the fast path for the common
        // re-add; the unique index ix_group_stories_group_id_story_id is the backstop for the concurrent
        // double add that passes it (caught below). Either way an already-added story is a no-op.
        bool alreadyAdded = await writeDb.GroupStories
            .AnyAsync(gs => gs.GroupId == dto.GroupId && gs.StoryId == dto.StoryId);
        if (alreadyAdded) return;

        GroupStory groupStory = new()
        {
            GroupId      = dto.GroupId,
            StoryId      = dto.StoryId,
            AddedByUserId = userId,
            DateAdded    = DateTime.UtcNow
        };
        writeDb.GroupStories.Add(groupStory);
        try
        {
            await writeDb.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsViolationOf(ex, PostgresErrorCodes.UniqueViolation, GroupStoryIndex))
        {
            // Another request added this story between the check and the insert: the add happened,
            // once. No folder assignment and no second fan-out. Detached so the circuit-scoped context
            // never retries the rejected row on its next save.
            writeDb.Entry(groupStory).State = EntityState.Detached;
            return;
        }

        // Tier 3: optional folder assignment.
        if (dto.GroupFolderId.HasValue)
        {
            await AssignStoryToFolderInternalAsync(
                groupStory.GroupStoryId, dto.GroupFolderId.Value, story.Rating, dto.GroupId);
        }

        // Notifications (best-effort post-commit) — only on a real add. Before the early returns above,
        // any member could re-add an already-added story and re-fire the whole fan-out (service audit
        // §2.8). An authorless story still notifies members; only the author's own type 25 needs
        // an author. Both types anchor on the GroupStory row (owner ruling D16).
        try
        {
            await notifications.NotifyNewGroupStoryAsync(
                dto.GroupId, groupStory.GroupStoryId, story.AuthorId, userId);
        }
        catch (Exception ex)
        {
            // Best-effort — notification failure must not roll back the primary action.
            logger.LogWarning(ex,
                "NewGroupStory notification failed for story {StoryId} added to group {GroupId}",
                dto.StoryId, dto.GroupId);
        }
    }

    public async Task RemoveStoryAsync(int groupStoryId)
    {
        int userId = ActiveUser.RequireUserId();

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
        int userId = ActiveUser.RequireUserId();

        GroupStory? gs = await writeDb.GroupStories
            .Include(gs => gs.Story)
            .FirstOrDefaultAsync(gs => gs.GroupStoryId == groupStoryId);
        if (gs is null) throw new KeyNotFoundException($"GroupStory {groupStoryId} not found.");

        await RequireAdminAsync(gs.GroupId, userId);

        Rating storyRating = gs.Story?.Rating ?? Rating.E;
        await AssignStoryToFolderInternalAsync(groupStoryId, groupFolderId, storyRating, gs.GroupId);
    }

    public async Task UnassignStoryFromFolderAsync(int groupStoryId, int groupFolderId)
    {
        int userId = ActiveUser.RequireUserId();

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

    // Folder rules (schema audit §2.4, service audit §2.10, WU-SchemaHardening): names are validated
    // and unique among siblings byte-wise, a parent is in the same group, and a delete moves the
    // sub-folders to the top level. layer2-services.md §"Group Folder Integrity".

    public async Task<int> CreateFolderAsync(CreateFolderDto dto)
    {
        int userId = ActiveUser.RequireUserId();
        await RequireAdminAsync(dto.GroupId, userId);

        List<string> errors = GroupFolderValidations.NameErrors(dto.Name);

        // Validate MaxRating ≤ group cap.
        Rating groupCap = await writeDb.Groups
            .Where(g => g.GroupId == dto.GroupId)
            .Select(g => g.MaxContentRating)
            .FirstOrDefaultAsync();
        if (dto.MaxRating > groupCap)
            errors.Add($"Folder MaxRating ({dto.MaxRating}) cannot exceed the group's MaxContentRating ({groupCap}).");

        if (errors.Count > 0) throw new GroupValidationException(errors);
        string name = dto.Name.Trim();

        // The parent must be a folder of this group. A nonexistent id and another group's id get the same
        // answer (the D3.1 shape in AssignStoryToFolderInternalAsync), so the response never discloses
        // that the id exists elsewhere. Before WU-SchemaHardening any id was stored unchecked.
        if (dto.ParentFolderId is int parentId
            && !await writeDb.GroupFolders.AnyAsync(f => f.GroupFolderId == parentId && f.GroupId == dto.GroupId))
            throw new KeyNotFoundException($"Folder {parentId} not found.");

        if (await SiblingNameTakenAsync(dto.GroupId, dto.ParentFolderId, name, exceptFolderId: null))
            throw new GroupValidationException([GroupFolderValidations.DuplicateName(name)]);

        GroupFolder folder = new()
        {
            GroupId        = dto.GroupId,
            ParentFolderId = dto.ParentFolderId,
            Name           = name,
            MaxRating      = dto.MaxRating,
            SortOrder      = dto.SortOrder
        };

        writeDb.GroupFolders.Add(folder);
        try
        {
            await writeDb.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsViolationOf(ex, PostgresErrorCodes.UniqueViolation, FolderNameIndex))
        {
            // A sibling with this name was created after the check: the same answer as the check.
            writeDb.Entry(folder).State = EntityState.Detached;
            throw new GroupValidationException([GroupFolderValidations.DuplicateName(name)]);
        }
        catch (DbUpdateException ex) when (IsViolationOf(ex, PostgresErrorCodes.ForeignKeyViolation, FolderParentForeignKey))
        {
            // The parent was deleted after the check: the same answer as a missing parent.
            writeDb.Entry(folder).State = EntityState.Detached;
            throw new KeyNotFoundException($"Folder {dto.ParentFolderId} not found.");
        }
        return folder.GroupFolderId;
    }

    public async Task RenameFolderAsync(int groupFolderId, string newName)
    {
        int userId = ActiveUser.RequireUserId();

        GroupFolder? folder = await writeDb.GroupFolders
            .FirstOrDefaultAsync(f => f.GroupFolderId == groupFolderId);
        if (folder is null) throw new KeyNotFoundException($"Folder {groupFolderId} not found.");

        await RequireAdminAsync(folder.GroupId, userId);

        List<string> errors = GroupFolderValidations.NameErrors(newName);
        if (errors.Count > 0) throw new GroupValidationException(errors);
        string name = newName.Trim();

        // Renaming a folder to its own name changes nothing.
        if (name == folder.Name) return;

        if (await SiblingNameTakenAsync(folder.GroupId, folder.ParentFolderId, name, exceptFolderId: folder.GroupFolderId))
            throw new GroupValidationException([GroupFolderValidations.DuplicateName(name)]);

        folder.Name = name;
        try
        {
            await writeDb.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsViolationOf(ex, PostgresErrorCodes.UniqueViolation, FolderNameIndex))
        {
            writeDb.Entry(folder).State = EntityState.Detached;
            throw new GroupValidationException([GroupFolderValidations.DuplicateName(name)]);
        }
    }

    public async Task DeleteFolderAsync(int groupFolderId)
    {
        int userId = ActiveUser.RequireUserId();

        var folder = await writeDb.GroupFolders
            .Where(f => f.GroupFolderId == groupFolderId)
            .Select(f => new { f.GroupId, f.Name })
            .FirstOrDefaultAsync();
        if (folder is null) throw new KeyNotFoundException($"Folder {groupFolderId} not found.");

        await RequireAdminAsync(folder.GroupId, userId);

        // The sub-folders move to the top level (the self-FK's ON DELETE SET NULL; spec §Group Tables).
        // Top-level names are unique, so a sub-folder named like an existing top-level folder would make
        // the database refuse the whole delete. Refuse it here first, naming each clash, and delete
        // nothing. Refusing rather than renaming is a default, not an owner ruling (roadmap row 22).
        var children = await writeDb.GroupFolders
            .Where(f => f.ParentFolderId == groupFolderId)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.GroupFolderId)
            .Select(f => new { f.GroupFolderId, f.Name })
            .ToListAsync();

        async Task<(List<string> Collisions, int NextTopLevelSortOrder)> CheckPromotionAsync()
        {
            var topLevel = await writeDb.GroupFolders
                .Where(f => f.GroupId == folder.GroupId && f.ParentFolderId == null && f.GroupFolderId != groupFolderId)
                .Select(f => new { f.Name, f.SortOrder })
                .ToListAsync();
            // Ordinal: the unique index compares byte-wise (case-insensitive names are D29, pending).
            HashSet<string> topLevelNames = topLevel.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
            List<string> collisions = children
                .Where(c => topLevelNames.Contains(c.Name))
                .Select(c => GroupFolderValidations.DeleteWouldDuplicate(folder.Name, c.Name))
                .ToList();
            return (collisions, topLevel.Count == 0 ? 0 : topLevel.Max(f => f.SortOrder) + 1);
        }

        (List<string> collisions, int nextSortOrder) = await CheckPromotionAsync();
        if (collisions.Count > 0) throw new GroupValidationException(collisions);

        IExecutionStrategy strategy = writeDb.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                // The hard-delete precedent (layer2-services.md §"Hard deletes of content parents"): the
                // write context is scoped per circuit and may still track a folder or a story's folder
                // set from an earlier action, which the set-based writes below would leave stale; and a
                // retried delegate must not inherit a failed attempt's tracked state.
                writeDb.ChangeTracker.Clear();
                await using IDbContextTransaction tx = await writeDb.Database.BeginTransactionAsync();

                // Promoted sub-folders sort after the existing top-level folders, in their own order,
                // instead of tie-sorting against them.
                for (int i = 0; i < children.Count; i++)
                {
                    int childId = children[i].GroupFolderId;
                    int sortOrder = nextSortOrder + i;
                    await writeDb.GroupFolders
                        .Where(f => f.GroupFolderId == childId)
                        .ExecuteUpdateAsync(s => s.SetProperty(f => f.SortOrder, sortOrder));
                }

                // Set-based, so the database's SET NULL moves the sub-folders up after this row is gone:
                // a sub-folder that shares its deleted parent's name takes the parent's top-level name
                // without tripping the unique index. The join rows (story assignments) cascade; the
                // stories stay in the group. A 0-row delete is a lost race with another delete.
                int deleted = await writeDb.GroupFolders
                    .Where(f => f.GroupFolderId == groupFolderId)
                    .ExecuteDeleteAsync();
                if (deleted == 0) throw new KeyNotFoundException($"Folder {groupFolderId} not found.");

                await tx.CommitAsync();
            });
        }
        catch (Exception ex) when (IsViolationOf(ex, PostgresErrorCodes.UniqueViolation, FolderNameIndex))
        {
            // A top-level folder with a promoted sub-folder's name was created after the check. The
            // transaction rolled back; answer as the check would have.
            (List<string> late, _) = await CheckPromotionAsync();
            throw new GroupValidationException(late.Count > 0 ? late : [GroupFolderValidations.DeleteWouldDuplicateAny(folder.Name)]);
        }
    }

    public async Task ReorderFolderAsync(int groupFolderId, int newSortOrder)
    {
        int userId = ActiveUser.RequireUserId();

        GroupFolder? folder = await writeDb.GroupFolders
            .FirstOrDefaultAsync(f => f.GroupFolderId == groupFolderId);
        if (folder is null) throw new KeyNotFoundException($"Folder {groupFolderId} not found.");

        await RequireAdminAsync(folder.GroupId, userId);

        folder.SortOrder = newSortOrder;
        await writeDb.SaveChangesAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// True when a sibling of the proposed folder already has <paramref name="name"/>: same group, same
    /// parent (null = the top level, which EF's null semantics compare as equal). <c>==</c> translates to
    /// Postgres <c>=</c>, the byte-wise comparison the unique index makes, so the check and the index
    /// agree exactly.
    /// </summary>
    private Task<bool> SiblingNameTakenAsync(int groupId, int? parentFolderId, string name, int? exceptFolderId) =>
        writeDb.GroupFolders.AnyAsync(f => f.GroupId == groupId
                                           && f.ParentFolderId == parentFolderId
                                           && f.Name == name
                                           && f.GroupFolderId != exceptFolderId);

    /// <summary>
    /// True when <paramref name="ex"/> (a <c>DbUpdateException</c> from <c>SaveChanges</c>, or the
    /// <see cref="PostgresException"/> a set-based write throws directly) is <paramref name="sqlState"/>
    /// on the named constraint. Matching the name keeps every other violation a genuine fault.
    /// </summary>
    private static bool IsViolationOf(Exception ex, string sqlState, string constraintName) =>
        (ex as PostgresException ?? ex.InnerException as PostgresException) is { } pg
        && pg.SqlState == sqlState
        && pg.ConstraintName == constraintName;


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

    private async Task AssignStoryToFolderInternalAsync(
        int groupStoryId, int groupFolderId, Rating storyRating, int expectedGroupId)
    {
        GroupFolder? folder = await writeDb.GroupFolders
            .FirstOrDefaultAsync(f => f.GroupFolderId == groupFolderId);
        if (folder is null) throw new KeyNotFoundException($"Folder {groupFolderId} not found.");

        // D3.1 (modernization-audit/deferred-work.md §7 "Missing cross-entity ownership
        // validation"): a folder id from a DIFFERENT group must be rejected exactly like a
        // nonexistent one — same exception, same message shape — so the response never discloses
        // whether the id exists in another group. Without this, an admin of Group A could file
        // Group A's story into Group B's folder just by knowing/guessing its id (2026-07-24).
        if (folder.GroupId != expectedGroupId)
            throw new KeyNotFoundException($"Folder {groupFolderId} not found.");

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
