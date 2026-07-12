using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The go-live sweep body (Feature 55, WU-Spotlight): placements whose window has opened
/// (<c>StartDate &lt;= now &lt; EndDate</c>) and which haven't been announced
/// (<c>GoLiveNotifiedUtc IS NULL</c>) get their <c>StorySpotlighted</c> /
/// <c>RecommendationSpotlighted</c> notifications, then the idempotency stamp. Notifications
/// fire at go-live, never at booking (settled 2026-07-11 — <c>audit/Spotlight.md</c>).
///
/// <para>Separate from <see cref="SpotlightGoLiveWorker"/> so integration tests drive the sweep
/// deterministically (the <c>SiteDailyStatAggregator</c> pattern; <c>TestAppFactory</c> removes
/// the timer worker). The stamp lands only after that placement's notifications succeed — a
/// failed placement is retried next sweep, and the notification create-core's dedup absorbs any
/// partial repeat. A placement whose whole window elapsed unnotified (app down) ages out of the
/// query silently — "your story WAS featured last week" is not a useful notification.</para>
/// </summary>
public sealed class SpotlightGoLiveSweeper(
    ApplicationDbContext writeDb,
    INotificationWriteService notifications,
    ILogger<SpotlightGoLiveSweeper> logger)
{
    /// <summary>Runs one sweep; returns how many placements were stamped.</summary>
    public async Task<int> SweepAsync(CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;

        var due = await writeDb.CommunitySpotlights
            .Where(cs => cs.StartDate <= now && cs.EndDate > now && cs.GoLiveNotifiedUtc == null)
            .Select(cs => new
            {
                cs.SpotlightId,
                cs.StoryId,
                cs.SponsoringUserId,
                StoryAuthorId = (int?)cs.Story.AuthorId, // write context — unfiltered ground truth
                RecommenderId = cs.Recommendation != null ? cs.Recommendation.RecommenderId : null
            })
            .ToListAsync(ct);

        int stamped = 0;
        foreach (var placement in due)
        {
            try
            {
                // Sponsor is the notification source (drop-self: a sponsor who attached their own
                // recommendation isn't notified about it). 0 = sponsor account deleted — matches
                // no real user id, so both notifications still deliver.
                int sourceUserId = placement.SponsoringUserId ?? 0;

                if (placement.StoryAuthorId is int authorId)
                    await notifications.NotifyStorySpotlightedAsync(authorId, sourceUserId, placement.StoryId);

                if (placement.RecommenderId is int recommenderId)
                    await notifications.NotifyRecommendationSpotlightedAsync(
                        recommenderId, sourceUserId, placement.StoryId);

                await writeDb.CommunitySpotlights
                    .Where(cs => cs.SpotlightId == placement.SpotlightId)
                    .ExecuteUpdateAsync(s => s.SetProperty(cs => cs.GoLiveNotifiedUtc, now), ct);
                stamped++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Unstamped — next sweep retries this placement; others still proceed.
                logger.LogError(ex, "Spotlight go-live notification failed for placement {SpotlightId}; will retry",
                    placement.SpotlightId);
            }
        }

        return stamped;
    }
}
