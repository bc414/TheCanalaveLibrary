using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Drains the <see cref="ReadingProgressBuffer"/> into one batched PostgreSQL upsert
/// (Feature 44 L2 — the signal-buffering pattern's flush half, layer2-services.md).
///
/// Batch mechanics: <c>unnest(array...)</c> feeding <c>INSERT … ON CONFLICT DO UPDATE</c> — the
/// PostgreSQL replacement for SQL-Server-era TVP+MERGE (<c>MERGE</c> would demand PG 15+; this
/// needs nothing). The upsert is <b>idempotent</b> (GREATEST / OR merge semantics), so the
/// EnableRetryOnFailure execution strategy can safely replay it.
///
/// Normally invoked by <see cref="ReadingProgressFlushWorker"/> on its cadence; tests call
/// <see cref="FlushAsync"/> directly for deterministic flushes (testing.md — the timer alone would
/// be flaky under the per-test Respawn reset).
/// </summary>
public sealed class ReadingProgressFlusher(
    ReadingProgressBuffer buffer,
    IServiceScopeFactory scopeFactory,
    ILogger<ReadingProgressFlusher> logger)
{
    // EXISTS guards drop pings whose chapter/user was deleted mid-read — without them one stale
    // ping would FK-fail the whole batch. is_read is computed in C# (same float comparison as the
    // old direct write), never re-derived in SQL where real→double promotion shifts the 0.9 edge.
    private const string UpsertSql =
        """
        INSERT INTO user_chapter_interactions (user_id, chapter_id, read_progress, last_interaction_date, is_read)
        SELECT x.user_id, x.chapter_id, x.read_progress, x.last_interaction_date, x.is_read
        FROM unnest(@user_ids, @chapter_ids, @progresses, @stamps, @is_reads)
             AS x(user_id, chapter_id, read_progress, last_interaction_date, is_read)
        WHERE EXISTS (SELECT 1 FROM chapters c WHERE c.chapter_id = x.chapter_id)
          AND EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u.id = x.user_id)
        ON CONFLICT (user_id, chapter_id) DO UPDATE SET
            read_progress         = GREATEST(user_chapter_interactions.read_progress, EXCLUDED.read_progress),
            last_interaction_date = GREATEST(user_chapter_interactions.last_interaction_date, EXCLUDED.last_interaction_date),
            is_read               = user_chapter_interactions.is_read OR EXCLUDED.is_read
        """;

    /// <summary>
    /// Drains the buffer and writes one batched upsert. Returns the number of coalesced entries
    /// written (0 = buffer was empty). On failure the batch is restored to the buffer for the next
    /// cycle and the exception propagates (the worker logs it; a direct test caller sees it).
    /// </summary>
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        List<(int UserId, int ChapterId, float MaxProgress, DateTime LastTimestampUtc)> batch = buffer.Drain();
        if (batch.Count == 0) return 0;

        using Activity? activity = CanalaveTelemetry.ReadingProgress.Source.StartActivity("ReadingProgress.Flush");
        activity?.SetTag("canalave.readingprogress.batch_size", batch.Count);
        long startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var userIds = new int[batch.Count];
            var chapterIds = new int[batch.Count];
            var progresses = new float[batch.Count];
            var stamps = new DateTime[batch.Count];
            var isReads = new bool[batch.Count];
            for (int i = 0; i < batch.Count; i++)
            {
                (userIds[i], chapterIds[i], progresses[i], stamps[i]) = batch[i];
                isReads[i] = batch[i].MaxProgress >= 0.9f;
            }

            // Fresh scope per flush: the singleton flusher must not capture a scoped DbContext.
            // ServerActiveUserContext's anonymous fallback makes background scopes safe (its doc).
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ApplicationDbContext writeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await writeDb.Database.ExecuteSqlRawAsync(
                UpsertSql,
                [
                    new NpgsqlParameter("user_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = userIds },
                    new NpgsqlParameter("chapter_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = chapterIds },
                    new NpgsqlParameter("progresses", NpgsqlDbType.Array | NpgsqlDbType.Real) { Value = progresses },
                    new NpgsqlParameter("stamps", NpgsqlDbType.Array | NpgsqlDbType.TimestampTz) { Value = stamps },
                    new NpgsqlParameter("is_reads", NpgsqlDbType.Array | NpgsqlDbType.Boolean) { Value = isReads },
                ],
                cancellationToken);

            CanalaveTelemetry.ReadingProgress.FlushBatchSize.Record(batch.Count);
            CanalaveTelemetry.ReadingProgress.FlushDuration.Record(
                Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            return batch.Count;
        }
        catch (Exception ex)
        {
            // Restore so the batch retries next cycle — a transient DB outage must not eat
            // more than the loss-window contract allows.
            buffer.Restore(batch);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex,
                "Reading-progress flush failed; {BatchSize} entries restored to the buffer for retry.",
                batch.Count);
            throw;
        }
    }
}
