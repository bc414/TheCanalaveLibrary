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
                OwnerUserName = l.User.UserName
            })
            .FirstOrDefaultAsync();

        if (header is null) return null;
        bool isOwner = header.UserId == ActiveUser.UserId;
        if (!header.IsPublic && !isOwner) return null;

        // Viewer-visible count — matches what GetListStoryIdsAsync returns this viewer. The
        // OWNER counts unfiltered (Personal plane, WU-AccessGate): their own list must show and
        // count their own M entries so they can see and manage them. Takedown stays filtered.
        IQueryable<Story> countStories = readDb.Stories;
        if (isOwner)
            countStories = countStories.IgnoreQueryFilters(["ContentRating"]); // elevated read: Personal plane (own list)
        int storyCount = await readDb.CustomListEntries
            .CountAsync(e => e.ListId == listId && countStories.Any(s => s.StoryId == e.StoryId));

        return new CustomListDetailDto(
            header.CustomListId,
            header.ListName,
            header.IsPublic,
            header.DateCreated,
            header.UserId,
            header.OwnerUserName ?? "(deleted user)",
            storyCount);
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
        bool isOwner = gate.UserId == ActiveUser.UserId;
        if (!gate.IsPublic && !isOwner) return [];

        // Join through the filtered Stories set so rating/takedown-hidden entries drop out here,
        // keeping pagination gap-free (GetListingsByIdsAsync would drop them post-page otherwise).
        // The OWNER joins rating-unfiltered (Personal plane, WU-AccessGate) — see GetListDetailAsync.
        IQueryable<Story> joinStories = readDb.Stories;
        if (isOwner)
            joinStories = joinStories.IgnoreQueryFilters(["ContentRating"]); // elevated read: Personal plane (own list)

        var rows =
            from e in readDb.CustomListEntries
            join s in joinStories on e.StoryId equals s.StoryId
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

    public async Task<IReadOnlyList<GatedMetadataDto>> GetListHiddenMatureAsync(int listId)
    {
        // Mature count-line disclosure for a public list viewed below the M ceiling
        // (WU-AccessGate): interstitial-grade metadata for the entries the rating filter hid.
        // The owner never needs this (their reads are Personal-plane unfiltered above).
        if (ActiveUser.MaxRating >= Rating.M) return [];

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        var gate = await readDb.CustomLists
            .Where(l => l.CustomListId == listId)
            .Select(l => new { l.IsPublic, l.UserId })
            .FirstOrDefaultAsync();
        if (gate is null) return [];
        if (!gate.IsPublic && gate.UserId != ActiveUser.UserId) return [];

        Rating ceiling = ActiveUser.MaxRating;
        // elevated read: disclosure metadata only (title/author/rating); IsTakenDown stays active.
        return await readDb.CustomListEntries
            .Where(e => e.ListId == listId)
            .Join(readDb.Stories.IgnoreQueryFilters(["ContentRating"]).Where(s => s.Rating > ceiling),
                e => e.StoryId, s => s.StoryId,
                (e, s) => new GatedMetadataDto(
                    RevealedEntityType.Story,
                    s.StoryId,
                    s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                    s.AuthorId,
                    s.Author != null ? s.Author.UserName : null,
                    s.Rating))
            .ToListAsync();
    }

    public async Task<List<CustomListSummaryDto>> GetPublicListsByUserAsync(int userId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Class-A: a user's public lists are profile-tab data; respect their ProfileVisibility
        // (WU-AccessGate Phase 1 — /api/custom-lists/public/{userId} is directly reachable).
        if (!await ProfileVisibilityGuard.IsProfileVisibleAsync(readDb, ActiveUser, userId))
            return [];

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
