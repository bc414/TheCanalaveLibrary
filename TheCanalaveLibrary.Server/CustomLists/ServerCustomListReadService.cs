using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Custom Lists (Feature 51, WU-CustomLists). Mirrors
/// <see cref="ServerSavedTagSelectionReadService"/>'s shape (protected <c>ActiveUser</c>/
/// <c>ReadDbFactory</c> so the derived write service can reuse them — see <c>layer2-services.md</c>
/// §"CS9107/CS9124"). Story visibility rides the read context's content-rating/takedown named query
/// filters: every story-touching projection goes through a filtered <c>Stories</c> subquery, so
/// counts and id lists never include entries the viewer couldn't open.
/// </summary>
public class ServerCustomListReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : ICustomListReadService
{
    protected IActiveUserContext ActiveUser { get; } = activeUser;
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    public async Task<List<CustomListSummaryDto>> GetMyListsAsync()
    {
        if (ActiveUser.UserId is not int userId) return [];

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        return await ProjectSummaries(readDb, readDb.CustomLists.Where(l => l.UserId == userId));
    }

    public async Task<CustomListDetailDto?> GetListDetailAsync(int listId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        var header = await readDb.CustomLists
            .Where(l => l.CustomListId == listId)
            .Select(l => new
            {
                l.CustomListId,
                l.ListName,
                l.IsPublic,
                l.DateCreated,
                l.UserId,
                OwnerUserName = l.User.UserName,
                // Viewer-visible count — the filtered Stories subquery applies the rating/takedown
                // named filters, matching what GetListStoryIdsAsync will return this viewer.
                StoryCount = l.CustomListEntries.Count(e => readDb.Stories.Any(s => s.StoryId == e.StoryId))
            })
            .FirstOrDefaultAsync();

        if (header is null) return null;
        if (!header.IsPublic && header.UserId != ActiveUser.UserId) return null;

        return new CustomListDetailDto(
            header.CustomListId,
            header.ListName,
            header.IsPublic,
            header.DateCreated,
            header.UserId,
            header.OwnerUserName ?? "(deleted user)",
            header.StoryCount);
    }

    public async Task<IReadOnlyList<int>> GetListStoryIdsAsync(int listId, CustomListSortEnum sort)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Same visibility gate as GetListDetailAsync — not-visible and missing are both empty.
        var gate = await readDb.CustomLists
            .Where(l => l.CustomListId == listId)
            .Select(l => new { l.IsPublic, l.UserId })
            .FirstOrDefaultAsync();
        if (gate is null) return [];
        if (!gate.IsPublic && gate.UserId != ActiveUser.UserId) return [];

        // Join through the filtered Stories set so rating/takedown-hidden entries drop out here,
        // keeping pagination gap-free (GetListingsByIdsAsync would drop them post-page otherwise).
        var rows =
            from e in readDb.CustomListEntries
            join s in readDb.Stories on e.StoryId equals s.StoryId
            where e.ListId == listId
            // Title lives on the StoryListing vertical partition; empty-string fallback keeps the
            // ORDER BY total when a listing row is missing.
            select new { e.StoryId, e.DateAdded, Title = s.StoryListing != null ? s.StoryListing.StoryTitle : "" };

        IQueryable<int> ordered = sort switch
        {
            CustomListSortEnum.DateAddedAsc =>
                rows.OrderBy(r => r.DateAdded).ThenBy(r => r.StoryId).Select(r => r.StoryId),
            CustomListSortEnum.TitleAsc =>
                rows.OrderBy(r => r.Title).ThenBy(r => r.StoryId).Select(r => r.StoryId),
            CustomListSortEnum.TitleDesc =>
                rows.OrderByDescending(r => r.Title).ThenBy(r => r.StoryId).Select(r => r.StoryId),
            _ /* DateAddedDesc */ =>
                rows.OrderByDescending(r => r.DateAdded).ThenBy(r => r.StoryId).Select(r => r.StoryId),
        };

        return await ordered.ToArrayAsync();
    }

    public async Task<List<CustomListSummaryDto>> GetPublicListsByUserAsync(int userId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        return await ProjectSummaries(
            readDb, readDb.CustomLists.Where(l => l.UserId == userId && l.IsPublic));
    }

    public async Task<List<CustomListMembershipDto>> GetMyListMembershipsAsync(int storyId)
    {
        if (ActiveUser.UserId is not int userId) return [];

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        return await readDb.CustomLists
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.ListName)
            .Select(l => new CustomListMembershipDto(
                l.CustomListId,
                l.ListName,
                l.CustomListEntries.Any(e => e.StoryId == storyId)))
            .ToListAsync();
    }

    // ── Shared projection ────────────────────────────────────────────────────────

    /// <summary>Newest-created-first summary projection with the viewer-visible story count.</summary>
    private static Task<List<CustomListSummaryDto>> ProjectSummaries(
        ReadOnlyApplicationDbContext readDb, IQueryable<CustomList> lists) =>
        lists
            .OrderByDescending(l => l.DateCreated)
            .ThenByDescending(l => l.CustomListId)
            .Select(l => new CustomListSummaryDto(
                l.CustomListId,
                l.ListName,
                l.IsPublic,
                l.DateCreated,
                l.CustomListEntries.Count(e => readDb.Stories.Any(s => s.StoryId == e.StoryId))))
            .ToListAsync();
}
