using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Custom Lists (Feature 51, WU-CustomLists). Inherits the
/// read path via primary-constructor chaining, mirroring
/// <see cref="ServerSavedTagSelectionWriteService"/>. Every mutation requires an authenticated
/// user; mutations of an existing list additionally require ownership. Adds are silent by design —
/// no author notification (see <see cref="ICustomListWriteService"/>). The write context is
/// unfiltered (sees ground truth), so story lookups here load regardless of the caller's rating
/// settings; the one place viewer-visibility matters — clone entry copying — deliberately goes
/// through the filtered READ context instead.
/// </summary>
public class ServerCustomListWriteService(
    ApplicationDbContext writeDb,
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser)
    : ServerCustomListReadService(readDbFactory, activeUser), ICustomListWriteService
{
    public async Task<int> CreateListAsync(string listName, bool isPublic)
    {
        int userId = RequireAuthenticatedUser();

        bool nameExists = await ListNameExistsAsync(userId, listName, excludeListId: null);
        int listCount = await writeDb.CustomLists.CountAsync(l => l.UserId == userId);
        List<string> errors = CustomListValidations.ValidateListName(listName, nameExists, listCount);
        if (errors.Count > 0) throw new CustomListValidationException(errors);

        CustomList list = new()
        {
            UserId = userId,
            ListName = listName.Trim(),
            IsPublic = isPublic,
            DateCreated = DateTime.UtcNow
        };

        writeDb.CustomLists.Add(list);
        await writeDb.SaveChangesAsync();

        return list.CustomListId;
    }

    public async Task RenameListAsync(int listId, string newListName)
    {
        int userId = RequireAuthenticatedUser();
        CustomList list = await RequireOwnedListAsync(listId, userId);

        bool nameExists = await ListNameExistsAsync(userId, newListName, excludeListId: listId);
        List<string> errors = CustomListValidations.ValidateListName(newListName, nameExists);
        if (errors.Count > 0) throw new CustomListValidationException(errors);

        list.ListName = newListName.Trim();
        await writeDb.SaveChangesAsync();
    }

    public async Task SetListVisibilityAsync(int listId, bool isPublic)
    {
        int userId = RequireAuthenticatedUser();
        CustomList list = await RequireOwnedListAsync(listId, userId);

        list.IsPublic = isPublic;
        await writeDb.SaveChangesAsync();
    }

    public async Task DeleteListAsync(int listId)
    {
        int userId = RequireAuthenticatedUser();
        CustomList list = await RequireOwnedListAsync(listId, userId);

        // Entries cascade (CustomListConfiguration).
        writeDb.CustomLists.Remove(list);
        await writeDb.SaveChangesAsync();
    }

    public async Task AddStoryAsync(int listId, int storyId)
    {
        int userId = RequireAuthenticatedUser();
        await RequireOwnedListAsync(listId, userId);

        // Write context is unfiltered — the story loads regardless of the caller's rating settings
        // (same posture as ServerGroupWriteService.AddStoryAsync; a user adds from a story page
        // they could open, so no rating ceiling re-check belongs here).
        bool storyExists = await writeDb.Stories.AnyAsync(s => s.StoryId == storyId);
        if (!storyExists) throw new KeyNotFoundException($"Story {storyId} not found.");

        // Idempotent — composite PK (ListId, StoryId).
        bool alreadyInList = await writeDb.CustomListEntries
            .AnyAsync(e => e.ListId == listId && e.StoryId == storyId);
        if (alreadyInList) return;

        writeDb.CustomListEntries.Add(new CustomListEntry
        {
            ListId = listId,
            StoryId = storyId,
            DateAdded = DateTime.UtcNow
        });
        await writeDb.SaveChangesAsync();
    }

    public async Task RemoveStoryAsync(int listId, int storyId)
    {
        int userId = RequireAuthenticatedUser();
        await RequireOwnedListAsync(listId, userId);

        CustomListEntry? entry = await writeDb.CustomListEntries
            .FirstOrDefaultAsync(e => e.ListId == listId && e.StoryId == storyId);
        if (entry is null) return; // idempotent

        writeDb.CustomListEntries.Remove(entry);
        await writeDb.SaveChangesAsync();
    }

    public async Task<int> CloneListAsync(int sourceListId)
    {
        int userId = RequireAuthenticatedUser();

        var source = await writeDb.CustomLists
            .Where(l => l.CustomListId == sourceListId)
            .Select(l => new { l.ListName, l.IsPublic, l.UserId })
            .FirstOrDefaultAsync();
        if (source is null)
            throw new CustomListValidationException(["The list you're cloning no longer exists."]);
        if (!source.IsPublic && source.UserId != userId)
            throw new CustomListValidationException(["That list is not public."]);

        int listCount = await writeDb.CustomLists.CountAsync(l => l.UserId == userId);
        if (listCount >= CustomListValidations.MaxListsPerUser)
            throw new CustomListValidationException(
                [$"You can have at most {CustomListValidations.MaxListsPerUser} lists."]);

        // Clone copies only cloner-VISIBLE entries (settled 2026-07-13): the filtered READ context
        // applies the rating/takedown filters for this viewer, so hidden stories never enter their
        // account. Deliberately not writeDb (which sees ground truth).
        int[] visibleStoryIds;
        await using (ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync())
        {
            visibleStoryIds = await (
                from e in readDb.CustomListEntries
                join s in readDb.Stories on e.StoryId equals s.StoryId
                where e.ListId == sourceListId
                select e.StoryId).ToArrayAsync();
        }

        HashSet<string> existingNames = (await writeDb.CustomLists
                .Where(l => l.UserId == userId)
                .Select(l => l.ListName)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string cloneName = existingNames.Contains(source.ListName)
            ? CustomListValidations.DisambiguateCloneName(source.ListName, existingNames)
            : source.ListName;

        DateTime now = DateTime.UtcNow; // entry stamps are the clone time, not the source's
        CustomList clone = new()
        {
            UserId = userId,
            ListName = cloneName,
            IsPublic = false, // sharing is not transitive — see ICustomListWriteService docs.
            DateCreated = now,
            CustomListEntries = [.. visibleStoryIds.Select(id => new CustomListEntry
            {
                StoryId = id,
                DateAdded = now
            })]
        };

        writeDb.CustomLists.Add(clone);
        await writeDb.SaveChangesAsync();

        return clone.CustomListId;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private int RequireAuthenticatedUser()
    {
        if (ActiveUser.UserId is not int id)
            throw new InvalidOperationException("This operation requires an authenticated user.");
        return id;
    }

    /// <summary>Loads a list by id or throws: <see cref="KeyNotFoundException"/> when it doesn't
    /// exist, <see cref="UnauthorizedAccessException"/> when it isn't the caller's.</summary>
    private async Task<CustomList> RequireOwnedListAsync(int listId, int userId)
    {
        CustomList? list = await writeDb.CustomLists
            .FirstOrDefaultAsync(l => l.CustomListId == listId);
        if (list is null) throw new KeyNotFoundException($"Custom list {listId} not found.");
        if (list.UserId != userId)
            throw new UnauthorizedAccessException("You must be the owner of this list.");
        return list;
    }

    private async Task<bool> ListNameExistsAsync(int userId, string listName, int? excludeListId)
    {
        string trimmed = listName?.Trim() ?? string.Empty;
        return await writeDb.CustomLists.AnyAsync(l =>
            l.UserId == userId &&
            (excludeListId == null || l.CustomListId != excludeListId) &&
            l.ListName.ToLower() == trimmed.ToLower());
    }
}
