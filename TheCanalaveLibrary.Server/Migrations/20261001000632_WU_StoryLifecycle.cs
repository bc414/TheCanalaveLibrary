using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU_StoryLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "published_date",
                table: "stories",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "submitted_date",
                table: "stories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "first_published_date",
                table: "chapters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "publish_date",
                table: "chapter_contents",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "approved_story_submissions",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "can_auto_approve",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // ── Data-preserving backfill (WU-StoryLifecycle; owner rulings D1/D2) ─────────────
            // One-shot data statements, not standing DDL — they do NOT join layer1-data-model.md's
            // re-append registry (a pre-launch InitialSchema regeneration correctly drops them).
            // ORDER MATTERS: submitted_date is copied from published_date BEFORE the never-published
            // rows have published_date nulled.

            // Keep the moderator queue's order: a pending story's creation stamp was its only date.
            migrationBuilder.Sql(
                "UPDATE stories SET submitted_date = published_date WHERE story_status_id = 1;");

            // D2: NULL = never published. Draft(0)/PendingApproval(1)/Rejected(8) rows carried a
            // creation stamp, not a publication date. Accepted loss: unpublish was not a SANCTIONED
            // move before this WU, but the old all-enum Status select let an author set any status,
            // so a Draft/Pending/Rejected row here may once have been live — and no column recorded
            // that. Such a row loses its date and, if republished, is stamped as new. Pre-launch dev
            // data only; from this WU on, PublishedDate IS NULL reliably means "never published".
            migrationBuilder.Sql(
                "UPDATE stories SET published_date = NULL WHERE story_status_id IN (0, 1, 8);");

            // D2 chapter anchor: a published chapter's earliest version date is the best available
            // evidence of when it first went live.
            migrationBuilder.Sql("""
                UPDATE chapters c SET first_published_date = COALESCE(
                    (SELECT MIN(cc.publish_date) FROM chapter_contents cc WHERE cc.chapter_id = c.chapter_id),
                    now())
                WHERE c.is_published;
                """);

            // Per-version provenance: a never-published chapter's versions were never readable.
            migrationBuilder.Sql("""
                UPDATE chapter_contents cc SET publish_date = NULL
                FROM chapters c
                WHERE c.chapter_id = cc.chapter_id AND NOT c.is_published;
                """);

            // D1: PostApprovalStatus must be an entry status (InProgress 2 / Completed 3 / OpenBeta 7)
            // for submit and approve to accept it; Draft (0) stays as "not chosen yet".
            migrationBuilder.Sql(
                "UPDATE story_details SET post_approval_status = 2 WHERE post_approval_status NOT IN (0, 2, 3, 7);");

            // Dev-data trust backfill: an author who already has published stories is treated as
            // approved (pre-launch, no real moderator history exists to preserve).
            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" u SET approved_story_submissions = s.n
                FROM (SELECT author_id, COUNT(*) AS n FROM stories
                      WHERE story_status_id BETWEEN 2 AND 7 AND author_id IS NOT NULL
                      GROUP BY author_id) s
                WHERE u.id = s.author_id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-fill the columns that go back to NOT NULL before EF's drops/alters run —
            // submitted_date is still present here, so a pending story gets its queue date back.
            migrationBuilder.Sql(
                "UPDATE stories SET published_date = COALESCE(published_date, submitted_date, now());");
            migrationBuilder.Sql(
                "UPDATE chapter_contents SET publish_date = COALESCE(publish_date, now());");

            migrationBuilder.DropColumn(
                name: "submitted_date",
                table: "stories");

            migrationBuilder.DropColumn(
                name: "first_published_date",
                table: "chapters");

            migrationBuilder.DropColumn(
                name: "approved_story_submissions",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "can_auto_approve",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<DateTime>(
                name: "published_date",
                table: "stories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "publish_date",
                table: "chapter_contents",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
