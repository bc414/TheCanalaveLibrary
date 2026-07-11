using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Series (Feature 9, WU41). Mirrors
/// <see cref="ServerGroupReadService"/>'s shape. A series row itself has no visibility filter — its
/// member stories are individually subject to the read context's <c>ContentRating</c>/<c>IsTakenDown</c>
/// filters when a caller hydrates ids into display DTOs. See <c>audit/Stories.md</c> Feature 9 WU41
/// settled note for the raw-vs-filtered counting rule this service implements.
/// </summary>
public class ServerSeriesReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : ISeriesReadService
{
    /// <summary>
    /// Exposed as a protected property so the derived write service can access the user context
    /// without double-capturing the constructor parameter (eliminates CS9107/CS9124 warnings).
    /// See <c>layer2-services.md</c> §"CS9107/CS9124."
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    /// <summary>
    /// Read contexts are created per method from this factory (`await using`) — see
    /// <c>layer2-services.md</c> §"Read-context concurrency: factory per method".
    /// </summary>
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    public async Task<SeriesDetailDto?> GetSeriesByIdAsync(int seriesId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        var row = await readDb.Series
            .Where(s => s.SeriesId == seriesId)
            .Select(s => new
            {
                s.SeriesId,
                s.Name,
                s.Description,
                s.AuthorId,
                AuthorName = s.Author != null ? s.Author.UserName : null,
                s.DateCreated,
                // Raw membership order — the page hydrates via IStoryReadService.GetListingsByIdsAsync,
                // which silently drops ids the viewer can't see (see Feature 9 WU41 settled note).
                OrderedStoryIds = s.SeriesEntries
                    .OrderBy(se => se.OrderIndex)
                    .Select(se => se.StoryId)
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (row is null) return null;

        return new SeriesDetailDto(
            row.SeriesId, row.Name, row.Description, row.AuthorId, row.AuthorName,
            row.DateCreated, row.OrderedStoryIds);
    }

    public async Task<IReadOnlyList<SeriesListingDto>> GetSeriesByAuthorAsync(int authorId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        List<SeriesListingDto> items = await readDb.Series
            .Where(s => s.AuthorId == authorId)
            .OrderByDescending(s => s.DateCreated)
            .Select(s => new SeriesListingDto(
                s.SeriesId,
                s.Name,
                s.Description,
                s.SeriesEntries.Count, // raw count — see Feature 9 WU41 settled note
                s.AuthorId,
                s.Author != null ? s.Author.UserName : null,
                s.DateCreated))
            .ToListAsync();

        return items;
    }

    public async Task<IReadOnlyList<StorySeriesMembershipDto>> GetMembershipsForStoryAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        List<int> seriesIds = await readDb.SeriesEntries
            .Where(se => se.StoryId == storyId)
            .Select(se => se.SeriesId)
            .ToListAsync();

        if (seriesIds.Count == 0) return [];

        // Per-series membership query (small N — a story is rarely in more than a handful of
        // series), same pragmatic in-memory-assembly style as ServerGroupReadService.BuildFolderTreeAsync.
        List<StorySeriesMembershipDto> result = [];
        foreach (int seriesId in seriesIds)
        {
            // Explicit join through Stories applies the viewer's ContentRating/IsTakenDown read
            // filters — Position/Count/Prev/Next must reflect only what the viewer can actually
            // reach (Feature 9 WU41 settled note, audit/Stories.md).
            var members = await (
                from se in readDb.SeriesEntries
                join s in readDb.Stories on se.StoryId equals s.StoryId
                where se.SeriesId == seriesId
                orderby se.OrderIndex
                select new
                {
                    se.StoryId,
                    Title = s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty
                }).ToListAsync();

            int index = members.FindIndex(m => m.StoryId == storyId);
            // Defensive: the viewer is looking at storyId right now, so it should be in the
            // visible set; skip if not (e.g. a race with a takedown between page load and this call).
            if (index < 0) continue;

            string? seriesName = await readDb.Series
                .Where(s => s.SeriesId == seriesId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();
            if (seriesName is null) continue;

            result.Add(new StorySeriesMembershipDto(
                seriesId,
                seriesName,
                index + 1,
                members.Count,
                index > 0 ? members[index - 1].StoryId : null,
                index > 0 ? members[index - 1].Title : null,
                index < members.Count - 1 ? members[index + 1].StoryId : null,
                index < members.Count - 1 ? members[index + 1].Title : null));
        }

        return result;
    }
}
