using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Drains the <see cref="UserActivityBuffer"/> into one batched upsert on <c>User.LastActiveUtc</c>
/// (WU-SiteDailyStat, Feature 62 L2 — the signal-buffering pattern's flush half, layer2-services.md;
/// sibling of <see cref="ReadingProgressFlusher"/>). <c>GREATEST</c> is Postgres's null-tolerant
/// max — a first-ever ping (existing value NULL) just adopts the new stamp, so the merge is
/// idempotent under an <c>EnableRetryOnFailure</c> replay.
/// </summary>
public sealed class UserActivityFlusher(
    UserActivityBuffer buffer,
    IServiceScopeFactory scopeFactory,
    ILogger<UserActivityFlusher> logger)
{
    // No EXISTS guard needed (unlike the INSERT-based flushers): this is an UPDATE ... FROM, which
    // naturally no-ops for a user_id that no longer exists (mid-window account deletion) rather
    // than failing the batch.
    private const string UpsertSql =
        """
        UPDATE "AspNetUsers" u
        SET last_active_utc = GREATEST(u.last_active_utc, x.last_active_utc)
        FROM unnest(@user_ids, @stamps) AS x(user_id, last_active_utc)
        WHERE u.id = x.user_id
        """;

    /// <summary>
    /// Drains the buffer and writes one batched upsert. Returns the number of distinct users
    /// written (0 = buffer was empty). On failure the batch is restored for the next cycle and the
    /// exception propagates (the worker logs it; a direct test caller sees it).
    /// </summary>
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        List<(int UserId, DateTime LastActiveUtc)> batch = buffer.Drain();
        if (batch.Count == 0) return 0;

        using Activity? activity = CanalaveTelemetry.UserActivity.Source.StartActivity("UserActivity.Flush");
        activity?.SetTag("canalave.useractivity.batch_size", batch.Count);
        long startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var userIds = new int[batch.Count];
            var stamps = new DateTime[batch.Count];
            for (int i = 0; i < batch.Count; i++)
                (userIds[i], stamps[i]) = batch[i];

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ApplicationDbContext writeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await writeDb.Database.ExecuteSqlRawAsync(
                UpsertSql,
                [
                    new NpgsqlParameter("user_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = userIds },
                    new NpgsqlParameter("stamps", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz) { Value = stamps },
                ],
                cancellationToken);

            CanalaveTelemetry.UserActivity.FlushBatchSize.Record(batch.Count);
            CanalaveTelemetry.UserActivity.FlushDuration.Record(
                Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            return batch.Count;
        }
        catch (Exception ex)
        {
            buffer.Restore(batch);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex,
                "User-activity flush failed; {BatchSize} entries restored to the buffer for retry.",
                batch.Count);
            throw;
        }
    }
}
