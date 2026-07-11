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

        List<int> ids = await readDb.SavedTagSelections
            .Where(s => s.UserId == userId && s.IsPublic)
            .OrderByDescending(s => s.DateCreated)
            .Select(s => s.SavedTagSelectionId)
            .ToListAsync();

        List<SavedTagSelectionDetailDto> result = [];
        foreach (int id in ids)
        {
            SavedTagSelectionDetailDto? detail = await HydrateDetailAsync(readDb, id);
            if (detail is not null) result.Add(detail);
        }

        return result;
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
