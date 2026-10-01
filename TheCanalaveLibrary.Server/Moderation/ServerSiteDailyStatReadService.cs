using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for the <c>/mod/stats</c> dashboard (Feature 62). Plain LINQ
/// over <see cref="ReadOnlyApplicationDbContext"/> — <c>SiteDailyStat</c> is the one Layer-8 table
/// with an EF model, so this reads exactly like every other read service (no raw SQL here; only
/// the aggregator's writes are raw SQL — see layer8-data-marts.md §"site_daily_stats").
/// <para>Both reads are moderator-only and gate here with the shared
/// <c>activeUser.RequireModerator()</c> (owner ruling D9's sweep, WU-ModerationIntegrity 2026-09-30):
/// <c>ModStatsPage</c>'s <c>[Authorize]</c> does not protect the circuit, and the endpoint policy is
/// only the edge half.</para>
/// </summary>
public class ServerSiteDailyStatReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : ISiteDailyStatReadService
{
    public async Task<SiteDailyStatDto?> GetLatestAsync(CancellationToken ct = default)
    {
        activeUser.RequireModerator();

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync(ct);
        return await readDb.SiteDailyStats
            .OrderByDescending(s => s.StatDate)
            .Select(ToDto)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<SiteDailyStatDto>> GetSeriesAsync(int days, CancellationToken ct = default)
    {
        activeUser.RequireModerator();

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync(ct);
        List<SiteDailyStatDto> descending = await readDb.SiteDailyStats
            .OrderByDescending(s => s.StatDate)
            .Take(days)
            .Select(ToDto)
            .ToListAsync(ct);
        descending.Reverse(); // oldest first — the order charts want
        return descending;
    }

    private static readonly System.Linq.Expressions.Expression<Func<SiteDailyStat, SiteDailyStatDto>> ToDto =
        s => new SiteDailyStatDto(
            s.StatDate, s.TotalUsers, s.TotalStories, s.TotalWords,
            s.NewUsers, s.NewStories, s.NewChapters, s.NewWords, s.NewComments, s.NewBlogPosts,
            s.NewGroups, s.NewFollows, s.NewRecommendationsWritten, s.NewRecommendationSuccesses,
            s.ReportsFiled, s.ReportsResolved, s.FavoritesAdded, s.ChaptersRead, s.StoryViews,
            s.ActiveUsers);
}
