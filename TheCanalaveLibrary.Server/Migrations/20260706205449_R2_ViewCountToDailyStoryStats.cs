using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// R2 (view counts): drops the never-incremented <c>view_count</c> columns (a high-frequency
    /// mutable counter on the hot <c>stories</c> read row is a write-amplification trap; the
    /// chapter/blog copies were dead schema) and creates <c>daily_story_stats</c> — the per-story,
    /// per-day accumulation target of the view-count signal buffer. Lifetime total = SUM.
    ///
    /// <c>daily_story_stats</c> is <b>migration-managed raw DDL with no EF model</b> (settled): it
    /// is an accumulated stat table — ground truth, NOT a rebuildable L8 mart — so its schema is
    /// migration-owned and its data backed up like any other ground truth, while staying outside
    /// the EF model like the marts. Partition-ready: the PK leads with <c>story_id</c> for the SUM
    /// read but includes <c>stat_date</c>, so declarative RANGE(stat_date) partitioning can be
    /// adopted later via table swap if volume warrants.
    /// </summary>
    public partial class R2_ViewCountToDailyStoryStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "view_count",
                table: "stories");

            migrationBuilder.DropColumn(
                name: "view_count",
                table: "chapter_contents");

            migrationBuilder.DropColumn(
                name: "view_count",
                table: "base_blog_posts");

            migrationBuilder.Sql(
                """
                CREATE TABLE daily_story_stats (
                    story_id   integer NOT NULL REFERENCES stories (story_id) ON DELETE CASCADE,
                    stat_date  date    NOT NULL,
                    view_count integer NOT NULL DEFAULT 0,
                    CONSTRAINT pk_daily_story_stats PRIMARY KEY (story_id, stat_date)
                );

                COMMENT ON TABLE daily_story_stats IS
                    'Per-story per-day view accumulation (Feature 45). Written by the view-count '
                    'signal buffer''s flush worker; lifetime total = SUM(view_count). Accumulated '
                    'stat table (ground truth, not a rebuildable mart) — migration-managed, no EF '
                    'model. Views are never a sort key (non-sortable informational metric).';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS daily_story_stats;");

            migrationBuilder.AddColumn<int>(
                name: "view_count",
                table: "stories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "view_count",
                table: "chapter_contents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "view_count",
                table: "base_blog_posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
