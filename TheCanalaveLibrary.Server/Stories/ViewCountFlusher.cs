using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Drains the <see cref="ViewCountBuffer"/> into one batched upsert on <c>daily_story_stats</c>
/// (Feature 45 L2 — the signal-buffering pattern's flush half; sibling of
/// <see cref="ReadingProgressFlusher"/>). The table is migration-managed raw DDL with no EF model —
/// an <b>accumulated stat table (ground truth, not a rebuildable mart)</b>; today's row per story
/// gains the window's coalesced views, and lifetime total = SUM over the story's rows.
///
/// Unlike R1's GREATEST/OR merge, the additive <c>+=</c> upsert is <b>not</b> idempotent under an
/// execution-strategy replay after a lost commit-ack — an accepted rare over-count on a metric that
/// is lossy/approximate by contract (and never a sort key).
/// </summary>
public sealed class ViewCountFlusher(
    ViewCountBuffer buffer,
    IServiceScopeFactory scopeFactory,
    ILogger<ViewCountFlusher> logger)
{
    // EXISTS guard drops views of stories deleted mid-window (mirrors the daily_story_stats FK,
    // which exists for CASCADE cleanup — the guard keeps one stale ping from failing the batch).
    //
    // It also carries the kind-(g) parent check (WU-ParentVisibility, settled 2026-07-26:
    // "validate at drain time"). RecordViewAsync is anonymous-reachable and buffer-only by design —
    // a per-request guard would add a database round-trip to the hottest endpoint on the site and
    // negate the buffer's whole reason for existing. Validating here costs nothing extra: the EXISTS
    // was already in the statement, so this is one more predicate on a query that had to run anyway.
    //
    // Only the CONFIDENTIALITY axis is meaningful at drain time. Flushes run in a background scope
    // with no viewer, so per-viewer consent (rating ceiling, reveals) has nobody to be evaluated
    // against — whereas "published and not taken down" is viewer-independent. DiscoveryMartSchema
    // .VisibleStory is the codebase's existing SQL spelling of exactly that predicate; reused rather
    // than restated so the two cannot drift.
    private static readonly string UpsertSql =
        $"""
        INSERT INTO daily_story_stats (story_id, stat_date, view_count)
        SELECT x.story_id, @stat_date, x.view_count
        FROM unnest(@story_ids, @view_counts) AS x(story_id, view_count)
        WHERE EXISTS (
            SELECT 1 FROM stories s
            WHERE s.story_id = x.story_id AND {DiscoveryMartSchema.VisibleStory})
        ON CONFLICT (story_id, stat_date) DO UPDATE SET
            view_count = daily_story_stats.view_count + EXCLUDED.view_count
        """;

    /// <summary>
    /// Drains the buffer and writes one batched upsert into today's (UTC) rows. Returns the number
    /// of distinct stories written (0 = empty). On failure the batch is restored for the next cycle
    /// and the exception propagates (worker logs it; a direct test caller sees it).
    /// </summary>
    public async Task<int> FlushAsync(CancellationToken cancellationToken = default)
    {
        List<(int StoryId, int Views)> batch = buffer.Drain();
        if (batch.Count == 0) return 0;

        using Activity? activity = CanalaveTelemetry.ViewCount.Source.StartActivity("ViewCount.Flush");
        activity?.SetTag("canalave.viewcount.batch_size", batch.Count);
        long startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var storyIds = new int[batch.Count];
            var viewCounts = new int[batch.Count];
            for (int i = 0; i < batch.Count; i++)
                (storyIds[i], viewCounts[i]) = batch[i];

            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ApplicationDbContext writeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await writeDb.Database.ExecuteSqlRawAsync(
                UpsertSql,
                [
                    new NpgsqlParameter("story_ids", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = storyIds },
                    new NpgsqlParameter("view_counts", NpgsqlDbType.Array | NpgsqlDbType.Integer) { Value = viewCounts },
                    new NpgsqlParameter("stat_date", NpgsqlDbType.Date) { Value = DateOnly.FromDateTime(DateTime.UtcNow) },
                ],
                cancellationToken);

            CanalaveTelemetry.ViewCount.FlushBatchSize.Record(batch.Count);
            CanalaveTelemetry.ViewCount.FlushDuration.Record(
                Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
            return batch.Count;
        }
        catch (Exception ex)
        {
            buffer.Restore(batch);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex,
                "View-count flush failed; {BatchSize} story entries restored to the buffer for retry.",
                batch.Count);
            throw;
        }
    }
}
