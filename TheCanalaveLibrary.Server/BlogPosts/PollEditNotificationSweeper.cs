using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The poll edit-notification sweep body (Feature 37, WU-Polls; settled 2026-07-12 —
/// <c>audit/BlogPosts.md</c> F37): material edits to a voted-on poll stamp
/// <c>BasePoll.LastEditedAt</c>; once a poll has been quiet for
/// <see cref="QuietPeriod"/> (no further edit), its current voters get one
/// <c>PollUpdated</c> notification for the whole burst, then <c>EditNotifiedAt</c> is stamped.
/// A poll re-edited after notification re-arms (<c>EditNotifiedAt &lt; LastEditedAt</c>).
///
/// <para>Separate from <see cref="PollEditNotificationWorker"/> so integration tests drive the
/// sweep deterministically (the <c>SpotlightGoLiveSweeper</c> pattern; <c>TestAppFactory</c>
/// removes the timer worker). The stamp lands only after that poll's notifications succeed —
/// a failed poll retries next sweep; the create-core's dedup absorbs partial repeats.</para>
/// </summary>
public sealed class PollEditNotificationSweeper(
    ApplicationDbContext writeDb,
    INotificationWriteService notifications,
    ILogger<PollEditNotificationSweeper> logger)
{
    /// <summary>Edit burst is considered over after this much quiet time (settled 2026-07-12).
    /// Public so integration tests arm the sweep relative to the real constant.</summary>
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromMinutes(30);

    /// <summary>Runs one sweep; returns how many polls were stamped.</summary>
    public async Task<int> SweepAsync(CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;
        DateTime cutoff = now - QuietPeriod;

        var due = await writeDb.Polls
            .Where(p => p.LastEditedAt != null && p.LastEditedAt <= cutoff
                        && (p.EditNotifiedAt == null || p.EditNotifiedAt < p.LastEditedAt))
            .Select(p => new
            {
                p.PollId,
                p.OwnerId,
                BlogPostId = p is BlogPostPoll ? (int?)((BlogPostPoll)p).BlogPostId : null,
                VoterIds = p.PollOptions
                    .SelectMany(o => o.Votes).Select(v => v.UserId).Distinct().ToArray(),
            })
            .ToListAsync(ct);

        int stamped = 0;
        foreach (var poll in due)
        {
            try
            {
                // Current voters only — a voter who retracted since the edit isn't notified.
                // Voters may legitimately be empty (every vote sat on a since-deleted option);
                // the stamp still lands so the poll doesn't rescan forever.
                if (poll.VoterIds.Length > 0)
                    await notifications.NotifyPollUpdatedAsync(
                        poll.OwnerId, poll.VoterIds, poll.BlogPostId ?? 0);

                await writeDb.Polls
                    .Where(p => p.PollId == poll.PollId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.EditNotifiedAt, now), ct);
                stamped++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Unstamped — next sweep retries this poll; others still proceed.
                logger.LogError(ex, "Poll edit notification failed for poll {PollId}; will retry",
                    poll.PollId);
            }
        }

        return stamped;
    }
}
