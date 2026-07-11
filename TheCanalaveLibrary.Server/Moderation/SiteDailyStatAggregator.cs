using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Computes and upserts one <c>site_daily_stats</c> row (Feature 62, WU-SiteDailyStat) for a
/// completed UTC day. Unlike the three discovery marts, this is <b>not</b> a staging-table swap —
/// one raw <c>INSERT … ON CONFLICT (stat_date) DO UPDATE</c> statement computes every counter via
/// scalar subqueries and writes the single row atomically. See <c>layer8-data-marts.md</c>
/// §"site_daily_stats" for the full column-by-column source audit and the stock-vs-flow rule.
///
/// Day boundaries are passed as explicit UTC <c>timestamptz</c> range parameters
/// (<c>@range_start</c> inclusive, <c>@range_end</c> exclusive), never a <c>col::date</c> cast —
/// casting a <c>timestamptz</c> to <c>date</c> uses the session's <c>TimeZone</c> setting, which is
/// not guaranteed to be UTC. Explicit ranges make the boundary correct regardless of session
/// timezone.
///
/// Scoped, deliberately separate from the hosted <see cref="SiteDailyStatWorker"/> so integration
/// tests and a <c>/dev</c> probe can trigger an upsert deterministically without hosting timing —
/// same split as <see cref="DiscoveryMartRebuilder"/>.
/// </summary>
public sealed class SiteDailyStatAggregator(ApplicationDbContext context)
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    // Same StoryStatusEnum bounds as DiscoveryMartSchema.VisibleStory (settled 2026-07-07:
    // InProgress=2 .. OpenBeta=7, Draft/PendingApproval/Rejected excluded) — kept as its own const
    // here (unaliased; this subquery has only one table in scope) rather than referencing that
    // constant, since a compile-time const interpolation can't strip its "s." alias prefix.
    private const string VisibleStoryPredicate = "is_taken_down = false AND story_status_id BETWEEN 2 AND 7";

    // language=sql
    private const string UpsertSql =
        $"""
        INSERT INTO site_daily_stats (
            stat_date, total_users, total_stories, total_words,
            new_users, new_stories, new_chapters, new_words, new_comments, new_blog_posts,
            new_groups, new_follows, new_recommendations_written, new_recommendation_successes,
            reports_filed, reports_resolved, favorites_added, chapters_read, story_views, active_users
        )
        SELECT
            @stat_date,
            (SELECT COUNT(*) FROM "AspNetUsers" WHERE created_utc < @range_end),
            (SELECT COUNT(*) FROM stories WHERE published_date < @range_end AND {VisibleStoryPredicate}),
            (SELECT COALESCE(SUM(word_count), 0) FROM stories),

            (SELECT COUNT(*) FROM "AspNetUsers" WHERE created_utc >= @range_start AND created_utc < @range_end),
            (SELECT COUNT(*) FROM stories WHERE published_date >= @range_start AND published_date < @range_end),
            (SELECT COUNT(*) FROM chapters c JOIN chapter_contents cc ON c.primary_content_id = cc.chapter_content_id
                WHERE c.is_published AND cc.publish_date >= @range_start AND cc.publish_date < @range_end),
            (SELECT COALESCE(SUM(cc.word_count), 0) FROM chapters c JOIN chapter_contents cc ON c.primary_content_id = cc.chapter_content_id
                WHERE c.is_published AND cc.publish_date >= @range_start AND cc.publish_date < @range_end),
            (
                (SELECT COUNT(*) FROM chapter_comments WHERE date_posted >= @range_start AND date_posted < @range_end)
              + (SELECT COUNT(*) FROM blog_post_comments WHERE date_posted >= @range_start AND date_posted < @range_end)
              + (SELECT COUNT(*) FROM group_comments WHERE date_posted >= @range_start AND date_posted < @range_end)
              + (SELECT COUNT(*) FROM user_profile_comments WHERE date_posted >= @range_start AND date_posted < @range_end)
            ),
            (
                (SELECT COUNT(*) FROM profile_blog_posts WHERE date_created >= @range_start AND date_created < @range_end)
              + (SELECT COUNT(*) FROM group_blog_posts WHERE date_created >= @range_start AND date_created < @range_end)
            ),
            (SELECT COUNT(*) FROM groups WHERE date_created >= @range_start AND date_created < @range_end),
            (SELECT COUNT(*) FROM followed_users WHERE date_followed >= @range_start AND date_followed < @range_end),
            (SELECT COUNT(*) FROM recommendations WHERE date_posted >= @range_start AND date_posted < @range_end),
            (SELECT COUNT(*) FROM recommendation_successes WHERE date_recorded >= @range_start AND date_recorded < @range_end),
            (SELECT COUNT(*) FROM reports WHERE date_reported >= @range_start AND date_reported < @range_end),
            (SELECT COUNT(*) FROM reports WHERE date_resolved >= @range_start AND date_resolved < @range_end),
            (SELECT COUNT(*) FROM user_story_interaction_dates
                WHERE (favorite_date >= @range_start AND favorite_date < @range_end)
                   OR (hidden_favorite_date >= @range_start AND hidden_favorite_date < @range_end)),
            (SELECT COUNT(*) FROM user_chapter_interactions
                WHERE last_interaction_date >= @range_start AND last_interaction_date < @range_end),
            (SELECT COALESCE(SUM(view_count), 0) FROM daily_story_stats WHERE stat_date = @stat_date),
            (SELECT COUNT(*) FROM "AspNetUsers" WHERE last_active_utc >= @range_start AND last_active_utc < @range_end)
        ON CONFLICT (stat_date) DO UPDATE SET
            total_users = EXCLUDED.total_users,
            total_stories = EXCLUDED.total_stories,
            total_words = EXCLUDED.total_words,
            new_users = EXCLUDED.new_users,
            new_stories = EXCLUDED.new_stories,
            new_chapters = EXCLUDED.new_chapters,
            new_words = EXCLUDED.new_words,
            new_comments = EXCLUDED.new_comments,
            new_blog_posts = EXCLUDED.new_blog_posts,
            new_groups = EXCLUDED.new_groups,
            new_follows = EXCLUDED.new_follows,
            new_recommendations_written = EXCLUDED.new_recommendations_written,
            new_recommendation_successes = EXCLUDED.new_recommendation_successes,
            reports_filed = EXCLUDED.reports_filed,
            reports_resolved = EXCLUDED.reports_resolved,
            favorites_added = EXCLUDED.favorites_added,
            chapters_read = EXCLUDED.chapters_read,
            story_views = EXCLUDED.story_views,
            active_users = EXCLUDED.active_users
        """;

    /// <summary>
    /// Upserts the row for <paramref name="statDate"/> (a completed UTC day). Idempotent — safe to
    /// call more than once for the same day (e.g. a retry, or the worker's bounded gap-fill).
    ///
    /// <b>Known one-time limitation:</b> <c>new_users</c> is computed from <c>User.CreatedUtc</c>,
    /// which is backfilled to this feature's migration-deploy instant for every pre-existing user
    /// (their real registration date is unrecoverable). If <paramref name="statDate"/> is the
    /// deploy day itself, this query counts the entire pre-existing user base as "new" that day —
    /// a one-time cosmetic spike on the dashboard's growth chart, not an ongoing correctness bug.
    /// Every subsequent day is accurate.
    /// </summary>
    public async Task UpsertDayAsync(DateOnly statDate, CancellationToken ct = default)
    {
        using Activity? activity = CanalaveTelemetry.Marts.Source.StartActivity("Marts.SiteDailyStatUpsert");
        activity?.SetTag("canalave.mart.name", "site_daily_stats");
        activity?.SetTag("canalave.sitedailystat.stat_date", statDate.ToString("yyyy-MM-dd"));

        long startTimestamp = Stopwatch.GetTimestamp();
        context.Database.SetCommandTimeout(CommandTimeout);

        DateTime rangeStart = statDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime rangeEnd = rangeStart.AddDays(1);

        try
        {
            await context.Database.ExecuteSqlRawAsync(
                UpsertSql,
                [
                    new NpgsqlParameter("stat_date", NpgsqlDbType.Date) { Value = statDate },
                    new NpgsqlParameter("range_start", NpgsqlDbType.TimestampTz) { Value = rangeStart },
                    new NpgsqlParameter("range_end", NpgsqlDbType.TimestampTz) { Value = rangeEnd },
                ],
                ct);

            double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            CanalaveTelemetry.Marts.RecordRebuild("site_daily_stats", durationMs, rows: 1, success: true);
        }
        catch (Exception ex)
        {
            double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            CanalaveTelemetry.Marts.RecordRebuild("site_daily_stats", durationMs, rows: 0, success: false);
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
