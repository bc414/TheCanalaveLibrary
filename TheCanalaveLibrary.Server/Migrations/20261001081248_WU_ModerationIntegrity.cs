using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU_ModerationIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Schema, part 1: the column and its FK (owner ruling D8) ─────────────────────────────
            migrationBuilder.AddColumn<int>(
                name: "reported_user_id",
                table: "reports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_reports_asp_net_users_reported_user_id",
                table: "reports",
                column: "reported_user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // ── Data (hand-written; runs before the unique index, which a dirty table would fail) ──
            // There is no production data; dev seed data exists and must survive. Status values:
            // 0 = Open, 1 = UnderReview, 2 = ResolvedNoAction (ReportStatusEnum).

            // 1. Backfill reported_user_id — the answerable account per target type (D8). A report filed
            //    before this column existed gets today's owner, the best snapshot still recoverable.
            migrationBuilder.Sql("""
                UPDATE reports r SET reported_user_id = u.id
                  FROM "AspNetUsers" u WHERE r.reported_entity_type = 0 AND u.id = r.reported_entity_id;
                UPDATE reports r SET reported_user_id = s.author_id
                  FROM stories s WHERE r.reported_entity_type = 1 AND s.story_id = r.reported_entity_id;
                UPDATE reports r SET reported_user_id = c.user_id
                  FROM base_comments c WHERE r.reported_entity_type = 2 AND c.comment_id = r.reported_entity_id;
                UPDATE reports r SET reported_user_id = b.author_id
                  FROM base_blog_posts b WHERE r.reported_entity_type = 3 AND b.blog_post_id = r.reported_entity_id;
                UPDATE reports r SET reported_user_id = x.recommender_id
                  FROM recommendations x WHERE r.reported_entity_type = 4 AND x.recommendation_id = r.reported_entity_id;
                UPDATE reports r SET reported_user_id = m.sender_user_id
                  FROM private_messages m WHERE r.reported_entity_type = 5 AND m.message_id = r.reported_entity_id;
                """);

            // 2. Close pre-existing zombies: open reports whose target row is gone (the D7 sub-edge's
            //    shape — ResolvedNoAction, no moderator, a note; ReportLedger does the same going forward).
            migrationBuilder.Sql("""
                UPDATE reports r
                   SET report_status_id = 2, moderator_user_id = NULL, date_resolved = now(),
                       action_taken = 'Closed by migration WU_ModerationIntegrity: target no longer exists.'
                 WHERE r.report_status_id IN (0, 1)
                   AND (   (r.reported_entity_type = 0 AND NOT EXISTS (SELECT 1 FROM "AspNetUsers" u WHERE u.id = r.reported_entity_id))
                        OR (r.reported_entity_type = 1 AND NOT EXISTS (SELECT 1 FROM stories s WHERE s.story_id = r.reported_entity_id))
                        OR (r.reported_entity_type = 2 AND NOT EXISTS (SELECT 1 FROM base_comments c WHERE c.comment_id = r.reported_entity_id))
                        OR (r.reported_entity_type = 3 AND NOT EXISTS (SELECT 1 FROM base_blog_posts b WHERE b.blog_post_id = r.reported_entity_id))
                        OR (r.reported_entity_type = 4 AND NOT EXISTS (SELECT 1 FROM recommendations x WHERE x.recommendation_id = r.reported_entity_id))
                        OR (r.reported_entity_type = 5 AND NOT EXISTS (SELECT 1 FROM private_messages m WHERE m.message_id = r.reported_entity_id)));
                """);

            // 3. Close duplicate open reports by one reporter on one target, keeping the oldest
            //    (MIN(report_id)). Without this the unique index below fails on a dirty dev DB.
            migrationBuilder.Sql("""
                UPDATE reports r
                   SET report_status_id = 2, moderator_user_id = NULL, date_resolved = now(),
                       action_taken = 'Closed by migration WU_ModerationIntegrity: duplicate open report by the same reporter.'
                 WHERE r.report_status_id IN (0, 1)
                   AND r.reporter_user_id IS NOT NULL
                   AND EXISTS (SELECT 1 FROM reports o
                                WHERE o.report_status_id IN (0, 1)
                                  AND o.reporter_user_id = r.reporter_user_id
                                  AND o.reported_entity_type = r.reported_entity_type
                                  AND o.reported_entity_id = r.reported_entity_id
                                  AND o.report_id < r.report_id);
                """);

            // 4. One-shot recompute of every ActiveReportCount from its ground truth (D7's definition, the
            //    D21 recompute expression): the open-report COUNT(*) per target. Heals the double-decrement
            //    and orphaned-+1 drift the old code allowed; the standing reconciler is WU-CounterSymmetry's.
            migrationBuilder.Sql("""
                UPDATE stories t SET active_report_count = c.n
                  FROM (SELECT s.story_id AS id, COUNT(r.report_id)::int AS n FROM stories s
                          LEFT JOIN reports r ON r.reported_entity_type = 1 AND r.reported_entity_id = s.story_id
                                             AND r.report_status_id IN (0, 1)
                         GROUP BY s.story_id) c
                 WHERE t.story_id = c.id AND t.active_report_count <> c.n;
                UPDATE "AspNetUsers" t SET active_report_count = c.n
                  FROM (SELECT u.id AS id, COUNT(r.report_id)::int AS n FROM "AspNetUsers" u
                          LEFT JOIN reports r ON r.reported_entity_type = 0 AND r.reported_entity_id = u.id
                                             AND r.report_status_id IN (0, 1)
                         GROUP BY u.id) c
                 WHERE t.id = c.id AND t.active_report_count <> c.n;
                UPDATE base_comments t SET active_report_count = c.n
                  FROM (SELECT b.comment_id AS id, COUNT(r.report_id)::int AS n FROM base_comments b
                          LEFT JOIN reports r ON r.reported_entity_type = 2 AND r.reported_entity_id = b.comment_id
                                             AND r.report_status_id IN (0, 1)
                         GROUP BY b.comment_id) c
                 WHERE t.comment_id = c.id AND t.active_report_count <> c.n;
                UPDATE base_blog_posts t SET active_report_count = c.n
                  FROM (SELECT b.blog_post_id AS id, COUNT(r.report_id)::int AS n FROM base_blog_posts b
                          LEFT JOIN reports r ON r.reported_entity_type = 3 AND r.reported_entity_id = b.blog_post_id
                                             AND r.report_status_id IN (0, 1)
                         GROUP BY b.blog_post_id) c
                 WHERE t.blog_post_id = c.id AND t.active_report_count <> c.n;
                UPDATE recommendations t SET active_report_count = c.n
                  FROM (SELECT x.recommendation_id AS id, COUNT(r.report_id)::int AS n FROM recommendations x
                          LEFT JOIN reports r ON r.reported_entity_type = 4 AND r.reported_entity_id = x.recommendation_id
                                             AND r.report_status_id IN (0, 1)
                         GROUP BY x.recommendation_id) c
                 WHERE t.recommendation_id = c.id AND t.active_report_count <> c.n;
                """);

            // ── Schema, part 2: the three indexes (D7/D8; pre-data by owner direction, unmeasured) ──
            migrationBuilder.CreateIndex(
                name: "ix_reports_open_reporter_target",
                table: "reports",
                columns: new[] { "reporter_user_id", "reported_entity_type", "reported_entity_id" },
                unique: true,
                filter: "\"report_status_id\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "ix_reports_open_target",
                table: "reports",
                columns: new[] { "reported_entity_type", "reported_entity_id" },
                filter: "\"report_status_id\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "ix_reports_reported_user_id",
                table: "reports",
                column: "reported_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Schema only. The data steps are not reversed: the zombie and duplicate closures and the
            // counter recompute were corrections, and reopening them would restore the broken state.
            migrationBuilder.DropForeignKey(
                name: "fk_reports_asp_net_users_reported_user_id",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_reports_open_reporter_target",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_reports_open_target",
                table: "reports");

            migrationBuilder.DropIndex(
                name: "ix_reports_reported_user_id",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "reported_user_id",
                table: "reports");
        }
    }
}
