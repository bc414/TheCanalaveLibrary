using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>Result of one <see cref="UserStatRecalculator.RecalculateAllAsync"/> pass.</summary>
public sealed record UserStatRecalcResult(int RowsInserted, long CountersCorrected);

/// <summary>
/// Recomputes <c>UserStat</c>'s denormalized counters from ground truth (Feature 58,
/// WU-UserStatRecalc) to correct the drift the real-time same-transaction <c>ExecuteUpdateAsync</c>
/// increment path (<c>layer2-services.md</c> "UserStats Updates") can accumulate — a lost update
/// under concurrent writers, a bug in one call site, a manual DB fix that skipped the counter.
/// Set-based raw SQL, one pair of <c>UPDATE ... FROM</c> statements per counter (mirrors
/// <see cref="SiteDailyStatAggregator"/>'s style), never a per-user loop.
///
/// <b>Scope is deliberately partial</b> — see <c>layer2-services.md</c> "Recalculation worker (F58)"
/// and <c>audit/Profiles.md</c> Feature 58 for the settled IN/DEFER/DROP counter split:
/// <list type="bullet">
/// <item>14 already-wired counters, recomputed with the <b>exact</b> formula their write service
/// maintains — a recompute that diverges from the wired formula would fight the increment path and
/// "correct" a value that was already right.</item>
/// <item>3 unwired-but-populated counters (<c>ChaptersRead</c>, <c>WordsRead</c>,
/// <c>RecommendationsFoundUseful</c>) whose ground-truth tables are already populated — this worker
/// is their first populator.</item>
/// <item>1 raw-SQL counter (<c>ViewsOnStories</c>) reading the <c>daily_story_stats</c> L8 mart
/// (no EF model exists for it).</item>
/// <item>2 newly-wired counters (WU-StatBadgeProducers): <c>AcknowledgedAsBetaReaderCount</c>
/// (Accepted <c>StoryAcknowledgment</c> rows, role Beta Reader) and
/// <c>AcknowledgedAsInspirationCount</c> (Approved "Inspired By" <c>StoryLineage</c> rows toward the
/// target author, anti-self-link guarded) — mirror their wired formulas exactly, same discipline as
/// the 14 above.</item>
/// <item>Deferred, not recomputed: <c>SpotlightCount</c> — producer deferred to tracker item B8
/// (Spotlight donation pipeline); recomputing it to 0 would mask the missing producer, not correct
/// drift.</item>
/// <item><c>ActiveReportCount</c> was dropped from <c>UserStat</c> entirely (orphaned duplicate,
/// never written) — nothing to recompute.</item>
/// </list>
///
/// A third step (<see cref="SyncBadgeEarnedCountsAsync"/>) drift-corrects <c>UserBadge.EarnedCount</c>
/// from the now-corrected <c>UserStat</c> columns for badges with an automated producer — a separate
/// table from the 18 <c>UserStat</c> columns above, so it isn't one of <see cref="CounterSpecs"/>.
/// Deliberately does NOT award missing badges: award stays the producers' job, and a recalc that
/// mints badges would hide a broken producer instead of surfacing it — the same reasoning that keeps
/// <c>SpotlightCount</c> deferred above.
///
/// Every counter UPDATE runs in two passes: one that corrects rows whose ground truth differs
/// (guarded by <c>IS DISTINCT FROM</c> so the rows-affected count is a genuine "drift corrected"
/// signal, not "rows visited"), and one that zeroes rows with **no** matching ground-truth rows at
/// all (a plain inner join would silently skip these — a user whose true count is 0 but whose
/// persisted value drifted positive would never be corrected otherwise).
///
/// Step 1 inserts any missing <c>UserStat</c> row before recomputing — no write path creates one at
/// user registration (checked: DataSeeder only seeds fixture users), so this is not just a safety
/// net but the primary mechanism by which most real users get a row at all. Real-time
/// <c>ExecuteUpdateAsync</c> increments are silent no-ops for users without one.
///
/// Scoped, deliberately separate from the hosted <see cref="UserStatRecalculationWorker"/> so
/// integration tests can trigger a pass deterministically without hosting timing — same split as
/// <see cref="DiscoveryMartRebuilder"/>/<see cref="SiteDailyStatAggregator"/>.
/// </summary>
public sealed class UserStatRecalculator(ApplicationDbContext context)
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(5);

    // Every non-PK column is NOT NULL with no SQL-level default (checked: InitialSchema's
    // CreateTable has no HasDefaultValue on any UserStat counter) — the insert must list every
    // column explicitly as 0, not rely on the database to fill them in.
    // language=sql
    private const string InsertMissingRowsSql =
        """
        INSERT INTO user_stats (
            user_id, stories_read, stories_in_progress, stories_ignored, chapters_read, words_read,
            recommendations_found_useful, stories_written, words_written, comments_written,
            recommendations_written, blog_posts_written, acknowledged_as_beta_reader_count,
            acknowledged_as_inspiration_count, follower_count,
            authors_followed, favorites_on_stories, views_on_stories, groups_joined,
            recommendations_received, recommendation_successes_earned, spotlight_count
        )
        SELECT u.id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        FROM "AspNetUsers" u
        LEFT JOIN user_stats us ON us.user_id = u.id
        WHERE us.user_id IS NULL
        """;

    // ── 14 already-wired counters — mirror the wired formula exactly ────────────────────────────

    // language=sql
    private const string StoriesReadAgg =
        "SELECT user_id, COUNT(*)::integer AS value FROM user_story_interactions WHERE is_completed GROUP BY user_id";

    // Mirrors ServerUserStoryInteractionWriteService's wired formula (hadStarted && !willBeCompleted)
    // exactly — deliberately does NOT also exclude IsIgnored (that exclusion is a *display* filter
    // on the ActivelyReading bookshelf tab, a different concept from this counter).
    // language=sql
    private const string StoriesInProgressAgg =
        "SELECT user_id, COUNT(*)::integer AS value FROM user_story_interactions WHERE has_started AND NOT is_completed GROUP BY user_id";

    // language=sql
    private const string StoriesIgnoredAgg =
        "SELECT user_id, COUNT(*)::integer AS value FROM user_story_interactions WHERE is_ignored GROUP BY user_id";

    // language=sql
    private const string StoriesWrittenAgg =
        "SELECT author_id AS user_id, COUNT(*)::integer AS value FROM stories WHERE author_id IS NOT NULL GROUP BY author_id";

    // language=sql
    private const string WordsWrittenAgg =
        "SELECT author_id AS user_id, SUM(word_count)::bigint AS value FROM stories WHERE author_id IS NOT NULL GROUP BY author_id";

    // Counts all extant rows regardless of IsTakenDown — moderation takedown doesn't delete the
    // row or decrement the wired counter, so the recompute doesn't exclude it either.
    // language=sql
    private const string CommentsWrittenAgg =
        "SELECT user_id, COUNT(*)::integer AS value FROM base_comments WHERE user_id IS NOT NULL GROUP BY user_id";

    // language=sql
    private const string RecommendationsWrittenAgg =
        "SELECT recommender_id AS user_id, COUNT(*)::integer AS value FROM recommendations WHERE recommender_id IS NOT NULL GROUP BY recommender_id";

    // language=sql
    private const string BlogPostsWrittenAgg =
        "SELECT author_id AS user_id, COUNT(*)::integer AS value FROM base_blog_posts WHERE author_id IS NOT NULL GROUP BY author_id";

    // language=sql
    private const string FollowerCountAgg =
        "SELECT followed_user_id AS user_id, COUNT(*)::integer AS value FROM followed_users GROUP BY followed_user_id";

    // language=sql
    private const string AuthorsFollowedAgg =
        "SELECT user_id, COUNT(*)::integer AS value FROM followed_users GROUP BY user_id";

    // Public IsFavorite only — never IsHiddenFavorite (matches the wired transition-delta path).
    // language=sql
    private const string FavoritesOnStoriesAgg =
        """
        SELECT s.author_id AS user_id, COUNT(*)::integer AS value
        FROM user_story_interactions usi
        JOIN stories s ON s.story_id = usi.story_id
        WHERE usi.is_favorite AND s.author_id IS NOT NULL
        GROUP BY s.author_id
        """;

    // language=sql
    private const string GroupsJoinedAgg =
        "SELECT user_id, COUNT(*)::integer AS value FROM group_members GROUP BY user_id";

    // language=sql
    private const string RecommendationsReceivedAgg =
        """
        SELECT s.author_id AS user_id, COUNT(*)::integer AS value
        FROM recommendations r
        JOIN stories s ON s.story_id = r.story_id
        WHERE s.author_id IS NOT NULL
        GROUP BY s.author_id
        """;

    // Anti-self-farm join, mirrors ServerRecommendationWriteService.RecordSuccessAsync exactly:
    // anonymous recommendations (null RecommenderId) drop out, and a reader confirming their own
    // recommendation does not count.
    // language=sql
    private const string RecommendationSuccessesEarnedAgg =
        """
        SELECT r.recommender_id AS user_id, COUNT(*)::integer AS value
        FROM recommendation_successes rs
        JOIN recommendations r ON r.recommendation_id = rs.recommendation_id
        WHERE r.recommender_id IS NOT NULL AND rs.user_id <> r.recommender_id
        GROUP BY r.recommender_id
        """;

    // ── 3 unwired-but-populated counters — this worker is their first populator ─────────────────

    // language=sql
    private const string ChaptersReadAgg =
        "SELECT user_id, COUNT(*)::integer AS value FROM user_chapter_interactions WHERE is_read GROUP BY user_id";

    // language=sql
    private const string WordsReadAgg =
        """
        SELECT uci.user_id, SUM(cc.word_count)::integer AS value
        FROM user_chapter_interactions uci
        JOIN chapters c ON c.chapter_id = uci.chapter_id
        JOIN chapter_contents cc ON cc.chapter_content_id = c.primary_content_id
        WHERE uci.is_read
        GROUP BY uci.user_id
        """;

    // Reader-side — distinct from RecommendationSuccessesEarned (author-side, anti-self-farm join
    // above). Every recorded success already implies the reader found it useful; no filter needed.
    // language=sql
    private const string RecommendationsFoundUsefulAgg =
        "SELECT user_id, COUNT(*)::integer AS value FROM recommendation_successes GROUP BY user_id";

    // ── 1 raw-SQL counter — daily_story_stats has no EF model ───────────────────────────────────

    // language=sql
    private const string ViewsOnStoriesAgg =
        """
        SELECT s.author_id AS user_id, SUM(dss.view_count)::bigint AS value
        FROM daily_story_stats dss
        JOIN stories s ON s.story_id = dss.story_id
        WHERE s.author_id IS NOT NULL
        GROUP BY s.author_id
        """;

    // ── 2 newly-wired counters (WU-StatBadgeProducers) ──────────────────────────────────────────

    // Role 1 = "Beta Reader" (StoryConfigurations.cs); StatusId 1 = Accepted
    // (StoryAcknowledgmentStatus). No self-credit exclusion needed — RequestAcknowledgmentAsync
    // rejects self-crediting outright, so no such row can exist in the table at all (contrast with
    // RecommendationSuccessesEarnedAgg above, whose self-farm row DOES get inserted and is excluded
    // only from the counter/badge check, not the insert).
    // language=sql
    private const string AcknowledgedAsBetaReaderCountAgg =
        """
        SELECT acknowledged_user_id AS user_id, COUNT(*)::integer AS value
        FROM story_acknowledgments
        WHERE status_id = 1 AND acknowledgment_role_id = 1
        GROUP BY acknowledged_user_id
        """;

    // RelationshipTypeId 1 = "Inspired By" (StoryConfigurations.cs); StatusId 1 = Approved
    // (StoryLineageStatus). Counted toward the TARGET story's author (the person who inspired).
    // Mirrors ServerStoryLineageWriteService.ApproveLineageAsync's producer guard exactly: unlike
    // acknowledgments, a self-owned lineage link (same author on both sides) auto-approves and DOES
    // exist in the table — IS DISTINCT FROM is the NULL-safe exclusion (a story with no author, e.g.
    // author account deleted, is never "self" and must not be silently dropped by a plain <>).
    // language=sql
    private const string AcknowledgedAsInspirationCountAgg =
        """
        SELECT target.author_id AS user_id, COUNT(*)::integer AS value
        FROM story_lineages sl
        JOIN stories source ON source.story_id = sl.source_story_id
        JOIN stories target ON target.story_id = sl.target_story_id
        WHERE sl.status_id = 1 AND sl.relationship_type_id = 1
          AND target.author_id IS NOT NULL
          AND source.author_id IS DISTINCT FROM target.author_id
        GROUP BY target.author_id
        """;

    private readonly record struct CounterSpec(string ColumnName, string AggregateSql);

    private static readonly CounterSpec[] CounterSpecs =
    [
        new("stories_read", StoriesReadAgg),
        new("stories_in_progress", StoriesInProgressAgg),
        new("stories_ignored", StoriesIgnoredAgg),
        new("stories_written", StoriesWrittenAgg),
        new("words_written", WordsWrittenAgg),
        new("comments_written", CommentsWrittenAgg),
        new("recommendations_written", RecommendationsWrittenAgg),
        new("blog_posts_written", BlogPostsWrittenAgg),
        new("follower_count", FollowerCountAgg),
        new("authors_followed", AuthorsFollowedAgg),
        new("favorites_on_stories", FavoritesOnStoriesAgg),
        new("groups_joined", GroupsJoinedAgg),
        new("recommendations_received", RecommendationsReceivedAgg),
        new("recommendation_successes_earned", RecommendationSuccessesEarnedAgg),
        new("chapters_read", ChaptersReadAgg),
        new("words_read", WordsReadAgg),
        new("recommendations_found_useful", RecommendationsFoundUsefulAgg),
        new("views_on_stories", ViewsOnStoriesAgg),
        new("acknowledged_as_beta_reader_count", AcknowledgedAsBetaReaderCountAgg),
        new("acknowledged_as_inspiration_count", AcknowledgedAsInspirationCountAgg),
    ];

    /// <summary>Badge key → the <c>UserStat</c> column that backs its <c>EarnedCount</c>. Only
    /// badges with an automated producer appear here — Patron/Architect/Artist are manual grants
    /// with no counter to sync against and are deliberately absent.</summary>
    private readonly record struct BadgeCounterSpec(string BadgeKey, string CounterColumn);

    private static readonly BadgeCounterSpec[] BadgeCounterSpecs =
    [
        new(SiteBadges.Recommender, "recommendation_successes_earned"),
        new(SiteBadges.BetaReader, "acknowledged_as_beta_reader_count"),
    ];

    /// <summary>
    /// Runs one full recalculation pass: inserts any missing <c>UserStat</c> rows, then corrects
    /// every in-scope counter. Idempotent — safe to call more than once (a no-op pass touches 0
    /// rows). Not wrapped in one big transaction: each per-counter statement is independently
    /// atomic and touches disjoint columns, so a mid-pass failure leaves already-corrected counters
    /// corrected rather than rolling them back — consistent with the worker's "previous values keep
    /// serving on failure, next scheduled pass retries" resilience contract.
    /// </summary>
    public async Task<UserStatRecalcResult> RecalculateAllAsync(CancellationToken ct = default)
    {
        using Activity? activity = CanalaveTelemetry.UserStatRecalc.Source.StartActivity("UserStatRecalc.Pass");
        long startTimestamp = Stopwatch.GetTimestamp();
        context.Database.SetCommandTimeout(CommandTimeout);

        try
        {
            int rowsInserted = await context.Database.ExecuteSqlRawAsync(InsertMissingRowsSql, ct);
            activity?.SetTag("canalave.userstatrecalc.rows_inserted", rowsInserted);

            long countersCorrected = 0;
            foreach (CounterSpec spec in CounterSpecs)
                countersCorrected += await ApplyCounterAsync(spec, ct);

            // Step 3 — sync UserBadge.EarnedCount from the now-corrected UserStat columns above.
            // Folded into the same signal as the UserStat counters: both are "denormalized value
            // corrected from ground truth," just on different tables.
            foreach (BadgeCounterSpec spec in BadgeCounterSpecs)
                countersCorrected += await SyncBadgeEarnedCountAsync(spec, ct);

            activity?.SetTag("canalave.userstatrecalc.counters_corrected", countersCorrected);

            double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            CanalaveTelemetry.UserStatRecalc.RecordPass(durationMs, rowsInserted + countersCorrected, success: true);

            return new UserStatRecalcResult(rowsInserted, countersCorrected);
        }
        catch (Exception ex)
        {
            double durationMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            CanalaveTelemetry.UserStatRecalc.RecordPass(durationMs, 0, success: false);
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Corrects one counter column against its aggregate. Two statements: the first fixes rows
    /// whose ground truth differs from the persisted value (an inner join against the aggregate,
    /// `IS DISTINCT FROM`-guarded so the rows-affected count means "actually changed"); the second
    /// zeroes rows with no matching ground-truth row at all — an inner join alone would silently
    /// skip a user who has drifted to a wrong positive value but has zero true occurrences (e.g.
    /// every favorite was removed), since they'd never appear in the aggregate's GROUP BY output.
    /// </summary>
    private async Task<long> ApplyCounterAsync(CounterSpec spec, CancellationToken ct)
    {
        string updateMatched =
            $"""
            UPDATE user_stats us
            SET {spec.ColumnName} = agg.value
            FROM ({spec.AggregateSql}) agg
            WHERE us.user_id = agg.user_id
              AND us.{spec.ColumnName} IS DISTINCT FROM agg.value
            """;

        string zeroUnmatched =
            $"""
            UPDATE user_stats us
            SET {spec.ColumnName} = 0
            WHERE us.{spec.ColumnName} <> 0
              AND NOT EXISTS (SELECT 1 FROM ({spec.AggregateSql}) agg WHERE agg.user_id = us.user_id)
            """;

        int matched = await context.Database.ExecuteSqlRawAsync(updateMatched, ct);
        int zeroed = await context.Database.ExecuteSqlRawAsync(zeroUnmatched, ct);
        return matched + zeroed;
    }

    /// <summary>
    /// Syncs one badge's <c>EarnedCount</c> from its backing <c>UserStat</c> column. No "zero
    /// unmatched" pass like <see cref="ApplyCounterAsync"/> — a <c>UserBadge</c> row only exists
    /// once earned, and this method never creates or removes one (award stays the producers' job;
    /// see the class doc). <c>IS DISTINCT FROM</c>-guarded so rows-affected stays a genuine
    /// "drift corrected" signal.
    /// </summary>
    private async Task<long> SyncBadgeEarnedCountAsync(BadgeCounterSpec spec, CancellationToken ct)
    {
        // BadgeKey is interpolated directly, not parameterized — same trust level as ColumnName
        // above (both come from this file's own hardcoded specs, never external input; matches
        // this file's existing style of interpolating trusted identifiers straight into the SQL).
        string sql =
            $"""
            UPDATE user_badges ub
            SET earned_count = us.{spec.CounterColumn}
            FROM user_stats us
            WHERE ub.user_id = us.user_id
              AND ub.badge_key = '{spec.BadgeKey}'
              AND ub.earned_count IS DISTINCT FROM us.{spec.CounterColumn}
            """;

        return await context.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
