using Microsoft.EntityFrameworkCore;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// The notification cleanup sweep body (Feature 57): read notifications older than
/// <see cref="RetentionPeriod"/> are deleted set-based. Unread notifications are kept
/// indefinitely — the user hasn't seen them yet, and the unread count must stay truthful.
///
/// <para>Separate from <see cref="NotificationCleanupWorker"/> so integration tests drive the
/// sweep deterministically (the <c>SpotlightGoLiveSweeper</c> pattern; <c>TestAppFactory</c>
/// removes the timer worker). No new index backs the <c>is_read + date_created</c> predicate:
/// the sweep runs daily against a table pruned to ≤60 days of read rows, so a scan is
/// negligible, and a dedicated partial index would tax every notification insert to save a
/// background job milliseconds.</para>
/// </summary>
public sealed class NotificationCleanupSweeper(
    ApplicationDbContext writeDb,
    ILogger<NotificationCleanupSweeper> logger)
{
    /// <summary>How long read notifications are kept (grid_axes.md #57). Public so
    /// integration tests age rows relative to the real constant.</summary>
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(60);

    /// <summary>Runs one sweep; returns how many notifications were deleted.</summary>
    public async Task<int> SweepAsync(CancellationToken ct = default)
    {
        DateTime cutoff = DateTime.UtcNow - RetentionPeriod;

        int deleted = await writeDb.Notifications
            .Where(n => n.IsRead && n.DateCreated < cutoff)
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation(
                "Notification cleanup deleted {Count} read notification(s) older than {RetentionDays} days",
                deleted, RetentionPeriod.Days);

        return deleted;
    }
}
