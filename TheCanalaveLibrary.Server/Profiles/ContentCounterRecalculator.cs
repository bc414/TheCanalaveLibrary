using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>Result of one <see cref="ContentCounterRecalculator.RecalculateAllAsync"/> pass.</summary>
public sealed record ContentCounterRecalcResult(long CountersCorrected);

/// <summary>
/// Recomputes the denormalized counters that live on content tables — the reconciler owner ruling D21
/// requires for every counter with ground truth but none of its own (WU-CounterSymmetry). The
/// <c>user_stats</c> columns have theirs in <see cref="UserStatRecalculator"/>; this is the sibling for
/// the other eleven:
/// <list type="bullet">
/// <item><c>like_count</c> on <c>base_comments</c>, <c>base_blog_posts</c> and <c>recommendations</c>
/// — the like rows.</item>
/// <item><c>recommendations.successful_rec_count</c> — the success rows. It mirrors the wired +1 per
/// success row, with no self-exclusion at rec level (<c>roadmap.md</c> row 18).</item>
/// <item><c>chapters.version_count</c> — the version rows.</item>
/// <item><c>stories.word_count</c> — the primary versions of the story's <b>published</b> chapters
/// (service audit §2.4.3), the same expression as
/// <c>ServerChapterWriteService.RefreshStoryWordCountAsync</c>.</item>
/// <item><c>active_report_count</c> on <c>"AspNetUsers"</c>, <c>stories</c>, <c>base_comments</c>,
/// <c>base_blog_posts</c> and <c>recommendations</c> — the Open/UnderReview reports on that target
/// (owner ruling D7's expression; it reads the partial index <c>ix_reports_open_target</c>).
/// <c>Message</c> reports have no column.</item>
/// </list>
///
/// Each counter is the question its column caches; the recompute is the definition and a wired path
/// that permanently disagrees is the defect (<c>layer2-services.md</c> §"Counter recompute principle").
/// Same two-statement shape as <see cref="UserStatRecalculator"/>: an <c>IS DISTINCT FROM</c>-guarded
/// match-and-correct pass, and a zero-unmatched pass for a row whose ground truth has no rows at all
/// (an inner join alone would never visit it). Rows-affected therefore means "actually corrected".
///
/// Runs <b>before</b> <see cref="UserStatRecalculator"/> in the worker, because <c>words_written</c>
/// sums the <c>stories.word_count</c> this pass corrects. Scoped and separate from the hosted
/// <see cref="UserStatRecalculationWorker"/> so integration tests drive it deterministically.
/// </summary>
public sealed class ContentCounterRecalculator(ApplicationDbContext context)
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    // ── like_count ×3 ─────────────────────────────────────────────────────────────────────────────

    // language=sql
    private const string CommentLikesAgg =
        "SELECT comment_id AS id, COUNT(*)::integer AS value FROM comment_likes GROUP BY comment_id";

    // language=sql
    private const string BlogPostLikesAgg =
        "SELECT blog_post_id AS id, COUNT(*)::integer AS value FROM blog_post_likes GROUP BY blog_post_id";

    // language=sql
    private const string RecommendationLikesAgg =
        "SELECT recommendation_id AS id, COUNT(*)::integer AS value FROM recommendation_likes GROUP BY recommendation_id";

    // ── successful_rec_count, version_count, word_count ───────────────────────────────────────────

    // language=sql
    private const string RecommendationSuccessesAgg =
        "SELECT recommendation_id AS id, COUNT(*)::integer AS value FROM recommendation_successes GROUP BY recommendation_id";

    // language=sql
    private const string ChapterVersionsAgg =
        "SELECT chapter_id AS id, COUNT(*)::integer AS value FROM chapter_contents GROUP BY chapter_id";

    // Published chapters only; the primary version's words. A draft chapter, an alternate version and
    // a chapter mid-create (NULL primary_content_id) contribute nothing.
    // language=sql
    private const string StoryWordsAgg =
        """
        SELECT c.story_id AS id, SUM(cc.word_count)::integer AS value
        FROM chapters c
        JOIN chapter_contents cc ON cc.chapter_content_id = c.primary_content_id
        WHERE c.is_published
        GROUP BY c.story_id
        """;

    // ── active_report_count ×5 — D7: COUNT(*) of Open (0) / UnderReview (1) reports per target ────

    private static string OpenReportsAgg(ReportedEntityType type) =>
        $"""
        SELECT reported_entity_id AS id, COUNT(*)::integer AS value
        FROM reports
        WHERE reported_entity_type = {(short)type} AND report_status_id IN (0, 1)
        GROUP BY reported_entity_id
        """;

    /// <summary>One counter: <paramref name="Table"/>.<paramref name="CounterColumn"/>, keyed by
    /// <paramref name="KeyColumn"/>, against an aggregate projecting <c>(id, value)</c>.</summary>
    private readonly record struct CounterSpec(string Table, string KeyColumn, string CounterColumn, string AggregateSql);

    private static readonly CounterSpec[] CounterSpecs =
    [
        new("base_comments", "comment_id", "like_count", CommentLikesAgg),
        new("base_blog_posts", "blog_post_id", "like_count", BlogPostLikesAgg),
        new("recommendations", "recommendation_id", "like_count", RecommendationLikesAgg),
        new("recommendations", "recommendation_id", "successful_rec_count", RecommendationSuccessesAgg),
        new("chapters", "chapter_id", "version_count", ChapterVersionsAgg),
        new("stories", "story_id", "word_count", StoryWordsAgg),
        new("\"AspNetUsers\"", "id", "active_report_count", OpenReportsAgg(ReportedEntityType.User)),
        new("stories", "story_id", "active_report_count", OpenReportsAgg(ReportedEntityType.Story)),
        new("base_comments", "comment_id", "active_report_count", OpenReportsAgg(ReportedEntityType.Comment)),
        new("base_blog_posts", "blog_post_id", "active_report_count", OpenReportsAgg(ReportedEntityType.BlogPost)),
        new("recommendations", "recommendation_id", "active_report_count", OpenReportsAgg(ReportedEntityType.Recommendation)),
    ];

    /// <summary>The number of counter specs — exposed so a test can assert every one was exercised.</summary>
    public static int CounterCount => CounterSpecs.Length;

    /// <summary>
    /// Runs one full pass over every content counter. Idempotent — a second pass corrects 0. Not one
    /// big transaction: each statement is independently atomic and touches one column, so a
    /// mid-pass failure leaves the counters already corrected corrected (the worker's "previous values
    /// keep serving, the next pass retries" contract).
    /// </summary>
    public async Task<ContentCounterRecalcResult> RecalculateAllAsync(CancellationToken ct = default)
    {
        // A span on the existing UserStatRecalc source — the same worker drives both passes, so no new
        // top-level source or meter (logging.md §"UserStatRecalc").
        using Activity? activity = CanalaveTelemetry.UserStatRecalc.Source.StartActivity("ContentCounterRecalc.Pass");
        context.Database.SetCommandTimeout(CommandTimeout);

        try
        {
            long countersCorrected = 0;
            foreach (CounterSpec spec in CounterSpecs)
                countersCorrected += await ApplyCounterAsync(spec, ct);

            activity?.SetTag("canalave.contentcounterrecalc.counters_corrected", countersCorrected);
            return new ContentCounterRecalcResult(countersCorrected);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Corrects one counter column against its aggregate: a match-and-correct pass guarded by
    /// <c>IS DISTINCT FROM</c>, then a zero-unmatched pass. Identifiers are interpolated straight into
    /// the SQL — all come from this file's own constant specs, never from input (the same trust level
    /// and style as <see cref="UserStatRecalculator"/>).
    /// </summary>
    private async Task<long> ApplyCounterAsync(CounterSpec spec, CancellationToken ct)
    {
        string updateMatched =
            $"""
            UPDATE {spec.Table} t
            SET {spec.CounterColumn} = agg.value
            FROM ({spec.AggregateSql}) agg
            WHERE t.{spec.KeyColumn} = agg.id
              AND t.{spec.CounterColumn} IS DISTINCT FROM agg.value
            """;

        string zeroUnmatched =
            $"""
            UPDATE {spec.Table} t
            SET {spec.CounterColumn} = 0
            WHERE t.{spec.CounterColumn} <> 0
              AND NOT EXISTS (SELECT 1 FROM ({spec.AggregateSql}) agg WHERE agg.id = t.{spec.KeyColumn})
            """;

        int matched = await context.Database.ExecuteSqlRawAsync(updateMatched, ct);
        int zeroed = await context.Database.ExecuteSqlRawAsync(zeroUnmatched, ct);
        return matched + zeroed;
    }
}
