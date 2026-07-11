using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Saved Tag Selections (Feature 15, WU43). Inherits the read
/// path via primary-constructor chaining, mirroring <see cref="ServerSeriesWriteService"/>. Every
/// mutation requires an authenticated user; update/delete additionally require ownership. No per-user
/// cap (deliberate — see <c>audit/Tags.md</c> Feature 15).
/// </summary>
public class ServerSavedTagSelectionWriteService(
    ApplicationDbContext writeDb,
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser)
    : ServerSavedTagSelectionReadService(readDbFactory, activeUser), ISavedTagSelectionWriteService
{
    public async Task<int> CreateAsync(SavedTagSelectionInput input)
    {
        int userId = RequireAuthenticatedUser();

        bool nicknameExists = await NicknameExistsAsync(userId, input.Nickname, excludeId: null);
        List<string> errors = input.CanSave(nicknameExists);
        if (errors.Count > 0) throw new SavedTagSelectionValidationException(errors);

        SavedTagSelection selection = new()
        {
            UserId = userId,
            Nickname = input.Nickname.Trim(),
            Description = NormalizeDescription(input.Description),
            IsPublic = input.IsPublic,
            DateCreated = DateTime.UtcNow,
            Entries = BuildEntries(input)
        };

        writeDb.SavedTagSelections.Add(selection);
        await writeDb.SaveChangesAsync();

        return selection.SavedTagSelectionId;
    }

    public async Task UpdateAsync(int id, SavedTagSelectionInput input)
    {
        int userId = RequireAuthenticatedUser();

        SavedTagSelection? selection = await writeDb.SavedTagSelections
            .Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.SavedTagSelectionId == id);
        if (selection is null) throw new KeyNotFoundException($"Saved tag selection {id} not found.");
        RequireOwner(selection, userId);

        bool nicknameExists = await NicknameExistsAsync(userId, input.Nickname, excludeId: id);
        List<string> errors = input.CanSave(nicknameExists);
        if (errors.Count > 0) throw new SavedTagSelectionValidationException(errors);

        selection.Nickname = input.Nickname.Trim();
        selection.Description = NormalizeDescription(input.Description);
        selection.IsPublic = input.IsPublic;

        // Replace entries wholesale, not a merge — see ISavedTagSelectionWriteService.UpdateAsync doc.
        writeDb.SavedTagSelectionEntries.RemoveRange(selection.Entries);
        selection.Entries = BuildEntries(input);

        await writeDb.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        int userId = RequireAuthenticatedUser();

        SavedTagSelection? selection = await writeDb.SavedTagSelections
            .FirstOrDefaultAsync(s => s.SavedTagSelectionId == id);
        if (selection is null) throw new KeyNotFoundException($"Saved tag selection {id} not found.");
        RequireOwner(selection, userId);

        // Entries cascade (SavedTagSelectionConfiguration).
        writeDb.SavedTagSelections.Remove(selection);
        await writeDb.SaveChangesAsync();
    }

    public async Task<int> CopyPublicSelectionAsync(int sourceId)
    {
        int userId = RequireAuthenticatedUser();

        SavedTagSelection? source = await writeDb.SavedTagSelections
            .Include(s => s.Entries)
            .FirstOrDefaultAsync(s => s.SavedTagSelectionId == sourceId);
        if (source is null)
            throw new SavedTagSelectionValidationException(
                ["The saved selection you're copying no longer exists."]);
        if (!source.IsPublic && source.UserId != userId)
            throw new SavedTagSelectionValidationException(["That saved selection is not public."]);

        HashSet<string> existingNicknames = (await writeDb.SavedTagSelections
                .Where(s => s.UserId == userId)
                .Select(s => s.Nickname)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string nickname = existingNicknames.Contains(source.Nickname)
            ? SavedTagSelectionValidations.DisambiguateCopyNickname(source.Nickname, existingNicknames)
            : source.Nickname;

        SavedTagSelection copy = new()
        {
            UserId = userId,
            Nickname = nickname,
            Description = source.Description,
            IsPublic = false, // sharing is not transitive — see ISavedTagSelectionWriteService docs.
            DateCreated = DateTime.UtcNow,
            Entries = [.. source.Entries.Select(e => new SavedTagSelectionEntry
            {
                TagId = e.TagId,
                IsExcluded = e.IsExcluded
            })]
        };

        writeDb.SavedTagSelections.Add(copy);
        await writeDb.SaveChangesAsync();

        return copy.SavedTagSelectionId;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private int RequireAuthenticatedUser()
    {
        if (ActiveUser.UserId is not int id)
            throw new InvalidOperationException("This operation requires an authenticated user.");
        return id;
    }

    private static void RequireOwner(SavedTagSelection selection, int userId)
    {
        if (selection.UserId != userId)
            throw new UnauthorizedAccessException("You must be the owner of this saved tag selection.");
    }

    private async Task<bool> NicknameExistsAsync(int userId, string nickname, int? excludeId)
    {
        string trimmed = nickname.Trim();
        return await writeDb.SavedTagSelections.AnyAsync(s =>
            s.UserId == userId &&
            (excludeId == null || s.SavedTagSelectionId != excludeId) &&
            s.Nickname.ToLower() == trimmed.ToLower());
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    /// <summary>
    /// Builds the entry set from an input DTO. Defensive dedup: a tag can only appear once per
    /// selection (unique <c>(SavedTagSelectionId, TagId)</c> index) — <c>TagFilter</c>'s cross-dedup
    /// already guarantees included/excluded never overlap, but included wins here if a caller ever
    /// violates that.
    /// </summary>
    private static List<SavedTagSelectionEntry> BuildEntries(SavedTagSelectionInput input)
    {
        HashSet<int> includedIds = [.. input.IncludedTagIds];

        List<SavedTagSelectionEntry> entries =
        [
            .. includedIds.Select(tagId => new SavedTagSelectionEntry { TagId = tagId, IsExcluded = false }),
            .. input.ExcludedTagIds.Distinct().Where(id => !includedIds.Contains(id))
                .Select(tagId => new SavedTagSelectionEntry { TagId = tagId, IsExcluded = true })
        ];

        return entries;
    }
}
