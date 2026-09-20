using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Saved Tag Selections (Feature 15, WU43). Mirrors
/// <see cref="ServerSeriesReadService"/>'s shape (protected <c>ActiveUser</c>/<c>ReadDbFactory</c> so
/// the derived write service can reuse them without double-capturing the constructor parameter — see
/// <c>layer2-services.md</c> §"CS9107/CS9124"). Tag chips are hydrated via a raw-`Tag`-row join so
/// <see cref="TagChipDto.SpriteIdentifier"/> comes along for render-time sprite resolution.
/// </summary>
public class ServerSavedTagSelectionReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : ISavedTagSelectionReadService
{
    protected IActiveUserContext ActiveUser { get; } = activeUser;
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    public async Task<List<SavedTagSelectionSummaryDto>> GetMySelectionsAsync(SavedTagSelectionSortEnum sort)
    {
        if (ActiveUser.UserId is not int userId) return [];

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        IQueryable<SavedTagSelection> query = readDb.SavedTagSelections.Where(s => s.UserId == userId);

        query = sort switch
        {
            SavedTagSelectionSortEnum.DateCreatedAsc => query.OrderBy(s => s.DateCreated),
            SavedTagSelectionSortEnum.NicknameAsc => query.OrderBy(s => s.Nickname),
            SavedTagSelectionSortEnum.NicknameDesc => query.OrderByDescending(s => s.Nickname),
            _ /* DateCreatedDesc */ => query.OrderByDescending(s => s.DateCreated),
        };

        return await query
            .Select(s => new SavedTagSelectionSummaryDto(
                s.SavedTagSelectionId,
                s.Nickname,
                s.Description,
                s.IsPublic,
                s.DateCreated,
                s.Entries.Count(e => !e.IsExcluded),
                s.Entries.Count(e => e.IsExcluded)))
            .ToListAsync();
    }

    public async Task<SavedTagSelectionDetailDto?> GetSelectionDetailAsync(int id)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await HydrateDetailAsync(readDb, id);
    }

    public async Task<List<SavedTagSelectionDetailDto>> GetPublicSelectionsByUserAsync(int userId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Class-A: public tag selections are profile-tab data; respect the owner's
        // ProfileVisibility (WU-AccessGate Phase 1 — the endpoint is now anonymous-callable).
        if (!await ProfileVisibilityGuard.IsProfileVisibleAsync(readDb, ActiveUser, userId))
            return [];

        // Two queries for the whole tab, never one pair per selection (MA-408): headers in date
        // order, then every selection's entries joined to Tag in a single pass, grouped in memory.
        // HydrateDetailAsync stays the single-selection path; its owner-or-public gate is subsumed
        // here by the IsPublic filter below.
        var headers = await readDb.SavedTagSelections
            .Where(s => s.UserId == userId && s.IsPublic)
            .OrderByDescending(s => s.DateCreated)
            .Select(s => new { s.SavedTagSelectionId, s.Nickname, s.Description, s.IsPublic, s.UserId })
            .ToListAsync();

        if (headers.Count == 0) return [];

        List<int> ids = [.. headers.Select(h => h.SavedTagSelectionId)];

        var rows = await (
            from e in readDb.SavedTagSelectionEntries
            join t in readDb.Tags on e.TagId equals t.TagId
            where ids.Contains(e.SavedTagSelectionId)
            select new { e.SavedTagSelectionId, e.IsExcluded, Chip = new TagChipDto
            {
                TagId = t.TagId,
                TagName = t.TagName,
                TagTypeId = t.TagTypeId,
                Description = t.Description,
                SpriteIdentifier = t.SpriteIdentifier
            }}).ToListAsync();

        var bySelection = rows.ToLookup(r => r.SavedTagSelectionId);

        return [.. headers.Select(h => new SavedTagSelectionDetailDto(
            h.SavedTagSelectionId,
            h.Nickname,
            h.Description,
            h.IsPublic,
            h.UserId,
            [.. bySelection[h.SavedTagSelectionId].Where(r => !r.IsExcluded).Select(r => r.Chip)],
            [.. bySelection[h.SavedTagSelectionId].Where(r => r.IsExcluded).Select(r => r.Chip)]))];
    }

    public async Task<SavedTagSelectionDetailDto?> GetPublicSelectionByIdAsync(int id)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        SavedTagSelectionDetailDto? detail = await HydrateDetailAsync(readDb, id);
        if (detail is null) return null;

        // Both permalink gates live here, not at the endpoint — the endpoint is anonymous-callable
        // and services are the single enforcement point (layer2-services.md). HydrateDetailAsync
        // allows owner-or-public; the permalink is public-only, so an owner's unpublished selection
        // is not reachable by link either.
        if (!detail.IsPublic) return null;

        // Class A: the owner's ProfileVisibility governs their profile-tab data, and a permalink is
        // just another path to it (WU-AccessGate Phase 1).
        if (!await ProfileVisibilityGuard.IsProfileVisibleAsync(readDb, ActiveUser, detail.OwnerUserId))
            return null;

        return detail;
    }

    // ── Shared hydration ─────────────────────────────────────────────────────────

    /// <summary>
    /// Loads one selection's header + its entries joined to <c>Tag</c> for chip data, then splits into
    /// included/excluded lists. Returns <c>null</c> when the selection doesn't exist, or exists but is
    /// neither owned by the active user nor public (visibility gate lives here so both
    /// <see cref="GetSelectionDetailAsync"/> and the profile tab share one rule).
    /// </summary>
    private async Task<SavedTagSelectionDetailDto?> HydrateDetailAsync(ReadOnlyApplicationDbContext readDb, int id)
    {
        var header = await readDb.SavedTagSelections
            .Where(s => s.SavedTagSelectionId == id)
            .Select(s => new { s.SavedTagSelectionId, s.Nickname, s.Description, s.IsPublic, s.UserId })
            .FirstOrDefaultAsync();

        if (header is null) return null;
        if (!header.IsPublic && header.UserId != ActiveUser.UserId) return null;

        var rows = await (
            from e in readDb.SavedTagSelectionEntries
            join t in readDb.Tags on e.TagId equals t.TagId
            where e.SavedTagSelectionId == id
            select new { e.IsExcluded, Chip = new TagChipDto
            {
                TagId = t.TagId,
                TagName = t.TagName,
                TagTypeId = t.TagTypeId,
                Description = t.Description,
                SpriteIdentifier = t.SpriteIdentifier
            }}).ToListAsync();

        return new SavedTagSelectionDetailDto(
            header.SavedTagSelectionId,
            header.Nickname,
            header.Description,
            header.IsPublic,
            header.UserId,
            [.. rows.Where(r => !r.IsExcluded).Select(r => r.Chip)],
            [.. rows.Where(r => r.IsExcluded).Select(r => r.Chip)]);
    }
}
