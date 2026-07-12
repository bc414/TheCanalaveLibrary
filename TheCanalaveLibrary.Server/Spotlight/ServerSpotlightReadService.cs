using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server read implementation for the Community Spotlight cluster (Feature 55, WU-Spotlight).
///
/// <para><b>Join-through-the-filtered-DbSet rule:</b> every read that surfaces a story joins
/// <c>readDb.Stories</c> explicitly, so the viewer's <c>ContentRating</c>/<c>IsTakenDown</c>
/// named filters drop invisible stories' rows (the <c>ServerStoryLineageReadService</c>
/// precedent) — no per-method rating logic here.</para>
///
/// <para><b>Composition, not duplication:</b> story cards come from
/// <see cref="IStoryReadService.GetListingsByIdsAsync"/>, recommendation DTOs from
/// <see cref="IRecommendationReadService.GetByIdAsync"/> — this service owns no story/rec
/// presentation projection (layer2-services.md §"Service Composition"; active placements are
/// bounded by the position count, so the per-rec calls are a handful at most).</para>
/// </summary>
public class ServerSpotlightReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser,
    IStoryReadService storyReadService,
    IRecommendationReadService recommendationReadService,
    ISiteSettingsReadService siteSettings) : ISpotlightReadService
{
    /// <summary>Exposed for the derived write service (CS9107 double-capture pattern).</summary>
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    /// <summary>Exposed for the derived write service (CS9107 double-capture pattern).</summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    /// <summary>Exposed for the derived write service (CS9107 double-capture pattern).</summary>
    protected ISiteSettingsReadService SiteSettings { get; } = siteSettings;

    public async Task<IReadOnlyList<SpotlightDisplayDto>> GetActiveSpotlightsAsync()
    {
        DateTime now = DateTime.UtcNow;

        List<PlacementRow> rows;
        await using (ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync())
        {
            rows = await (
                from cs in readDb.CommunitySpotlights
                join s in readDb.Stories on cs.StoryId equals s.StoryId // filtered DbSet — invisible stories drop the row
                where cs.StartDate <= now && now < cs.EndDate
                orderby cs.StartDate, cs.SpotlightId
                select new PlacementRow(cs.SpotlightId, cs.StoryId, cs.RecommendationId, cs.StartDate, cs.EndDate)
            ).ToListAsync();
        }

        if (rows.Count == 0) return [];

        // Compose presentation: standard listing cards (one batched call) + per-placement rec DTOs
        // (bounded by the position count; GetByIdAsync returns null for non-Approved/taken-down —
        // exactly the blank-rec display state).
        StoryListingDto[] cards = await storyReadService.GetListingsByIdsAsync(
            rows.Select(r => r.StoryId).Distinct().ToArray());
        Dictionary<int, StoryListingDto> cardsById = cards.ToDictionary(c => c.StoryId);

        var result = new List<SpotlightDisplayDto>(rows.Count);
        foreach (PlacementRow row in rows)
        {
            if (!cardsById.TryGetValue(row.StoryId, out StoryListingDto? card))
                continue; // dropped by the presentation projection — mirror the join's filtering

            RecommendationDto? rec = row.RecommendationId is int recId
                ? await recommendationReadService.GetByIdAsync(recId)
                : null;

            result.Add(new SpotlightDisplayDto(row.SpotlightId, row.StartDate, row.EndDate, card, rec));
        }

        return result;
    }

    public async Task<IReadOnlyList<SpotlightSlotDto>> GetMyAvailableSlotsAsync()
    {
        if (ActiveUser.UserId is not int userId) return [];

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await readDb.SpotlightSlots
            .Where(s => s.GrantedToUserId == userId && s.Status == SpotlightSlotStatus.Available)
            .OrderBy(s => s.GrantedUtc)
            .Select(s => new SpotlightSlotDto(s.SlotId, s.Source, s.GrantedUtc))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<SpotlightBookingDto>> GetMyBookingsAsync()
    {
        if (ActiveUser.UserId is not int userId) return [];

        DateTime now = DateTime.UtcNow;
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await (
            from cs in readDb.CommunitySpotlights
            join s in readDb.Stories on cs.StoryId equals s.StoryId
            where cs.SponsoringUserId == userId && cs.EndDate > now
            orderby cs.StartDate
            select new SpotlightBookingDto(
                cs.SpotlightId,
                cs.StoryId,
                s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                cs.RecommendationId != null,
                cs.StartDate,
                cs.EndDate)
        ).ToListAsync();
    }

    public async Task<IReadOnlyList<SpotlightPickCandidateDto>> GetMyPickCandidatesAsync()
    {
        if (ActiveUser.UserId is not int userId) return [];

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await (
            from r in readDb.Recommendations // IsTakenDown filter applies
            join s in readDb.Stories on r.StoryId equals s.StoryId // invisible stories drop out
            where r.RecommenderId == userId
                  && r.StatusId == (short)RecommendationStatusEnum.Approved
            orderby r.DatePosted descending
            select new SpotlightPickCandidateDto(
                r.RecommendationId,
                r.StoryId,
                s.StoryListing != null ? s.StoryListing.StoryTitle : string.Empty,
                s.Author != null ? s.Author.UserName : null,
                r.IsHiddenGem,
                r.DatePosted)
        ).ToListAsync();
    }

    public async Task<IReadOnlyList<SpotlightBlockDto>> GetBlockAvailabilityAsync()
    {
        int durationDays = await SiteSettings.GetIntAsync(
            SiteSettingKeys.SpotlightBlockDurationDays, SiteSettingKeys.SpotlightBlockDurationDaysDefault);
        int horizonDays = await SiteSettings.GetIntAsync(
            SiteSettingKeys.SpotlightBookingHorizonDays, SiteSettingKeys.SpotlightBookingHorizonDaysDefault);
        int capacity = await SiteSettings.GetIntAsync(
            SiteSettingKeys.SpotlightPositionCount, SiteSettingKeys.SpotlightPositionCountDefault);

        DateTime now = DateTime.UtcNow;
        IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> blocks =
            SpotlightBlocks.BookableBlocks(now, durationDays, horizonDays);
        if (blocks.Count == 0) return [];

        DateTime windowStart = blocks[0].StartUtc;
        DateTime windowEnd = blocks[^1].EndUtc;

        // One query for every placement touching the window; occupancy per block is counted in
        // memory (block count is small — horizon/duration). Deliberately unfiltered by viewer
        // visibility: capacity is a physical property of the calendar, not a per-viewer view.
        List<(DateTime Start, DateTime End)> booked;
        await using (ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync())
        {
            booked = (await readDb.CommunitySpotlights
                    .Where(cs => cs.StartDate < windowEnd && cs.EndDate > windowStart)
                    .Select(cs => new { cs.StartDate, cs.EndDate })
                    .ToListAsync())
                .Select(x => (x.StartDate, x.EndDate))
                .ToList();
        }

        return blocks
            .Select(b => new SpotlightBlockDto(
                b.StartUtc,
                b.EndUtc,
                booked.Count(p => p.Start < b.EndUtc && p.End > b.StartUtc),
                capacity))
            .ToList();
    }

    private sealed record PlacementRow(
        int SpotlightId, int StoryId, int? RecommendationId, DateTime StartDate, DateTime EndDate);
}
