using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// WU-SiteDailyStat (Feature 62): adds <c>User.CreatedUtc</c> (sources new_users/total_users;
    /// existing rows backfill to this migration's deploy timestamp via
    /// <c>defaultValueSql: "CURRENT_TIMESTAMP"</c> — the real registration date is unrecoverable)
    /// and <c>User.LastActiveUtc</c> (nullable; stamped go-forward only by the
    /// <c>UserActivityBuffer</c> signal buffer). Also creates <c>site_daily_stats</c> — the one
    /// Layer-8 table with an EF model (append-only ground truth, not a rebuildable mart; see
    /// <c>layer8-data-marts.md</c> §"site_daily_stats"). The daily worker writes every row via raw
    /// <c>INSERT … ON CONFLICT</c>, never through this migration's DbSet.
    /// </summary>
    public partial class AddSiteDailyStatAndUserActivityColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "created_utc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_active_utc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "site_daily_stats",
                columns: table => new
                {
                    stat_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_users = table.Column<int>(type: "integer", nullable: false),
                    total_stories = table.Column<int>(type: "integer", nullable: false),
                    total_words = table.Column<long>(type: "bigint", nullable: false),
                    new_users = table.Column<int>(type: "integer", nullable: true),
                    new_stories = table.Column<int>(type: "integer", nullable: false),
                    new_chapters = table.Column<int>(type: "integer", nullable: false),
                    new_words = table.Column<long>(type: "bigint", nullable: false),
                    new_comments = table.Column<int>(type: "integer", nullable: false),
                    new_blog_posts = table.Column<int>(type: "integer", nullable: false),
                    new_groups = table.Column<int>(type: "integer", nullable: false),
                    new_follows = table.Column<int>(type: "integer", nullable: false),
                    new_recommendations_written = table.Column<int>(type: "integer", nullable: false),
                    new_recommendation_successes = table.Column<int>(type: "integer", nullable: false),
                    reports_filed = table.Column<int>(type: "integer", nullable: false),
                    reports_resolved = table.Column<int>(type: "integer", nullable: false),
                    favorites_added = table.Column<int>(type: "integer", nullable: false),
                    chapters_read = table.Column<int>(type: "integer", nullable: false),
                    story_views = table.Column<long>(type: "bigint", nullable: false),
                    active_users = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_daily_stats", x => x.stat_date);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_created_utc",
                table: "AspNetUsers",
                column: "created_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_daily_stats");

            migrationBuilder.DropIndex(
                name: "ix_users_created_utc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "created_utc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "last_active_utc",
                table: "AspNetUsers");
        }
    }
}
