using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU_StatBadgeProducers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_story_acknowledgments_acknowledged_user_id",
                table: "story_acknowledgments");

            migrationBuilder.DeleteData(
                table: "acknowledgment_roles",
                keyColumn: "acknowledgment_role_id",
                keyValue: (short)5);

            migrationBuilder.DeleteData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "RecommenderSilver");

            migrationBuilder.AddColumn<int>(
                name: "earned_count",
                table: "user_badges",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "date_responded",
                table: "story_acknowledgments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "status_id",
                table: "story_acknowledgments",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.UpdateData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "Recommender",
                column: "description",
                value: "A reader followed your recommendation and found the story genuinely helpful.");

            migrationBuilder.CreateIndex(
                name: "ix_story_acknowledgments_acknowledged_user_status",
                table: "story_acknowledgments",
                columns: new[] { "acknowledged_user_id", "status_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_story_acknowledgments_acknowledged_user_status",
                table: "story_acknowledgments");

            migrationBuilder.DropColumn(
                name: "earned_count",
                table: "user_badges");

            migrationBuilder.DropColumn(
                name: "date_responded",
                table: "story_acknowledgments");

            migrationBuilder.DropColumn(
                name: "status_id",
                table: "story_acknowledgments");

            migrationBuilder.InsertData(
                table: "acknowledgment_roles",
                columns: new[] { "acknowledgment_role_id", "role_name" },
                values: new object[] { (short)5, "Inspiration" });

            migrationBuilder.UpdateData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "Recommender",
                column: "description",
                value: "10+ readers followed your recommendation and found the story genuinely helpful.");

            migrationBuilder.InsertData(
                table: "badges",
                columns: new[] { "badge_key", "description", "display_name", "icon_base_url", "sort_order" },
                values: new object[] { "RecommenderSilver", "50+ readers followed your recommendation and found the story genuinely helpful.", "Recommender (Silver)", "icons/badges/recommender_silver.png", 30 });

            migrationBuilder.CreateIndex(
                name: "ix_story_acknowledgments_acknowledged_user_id",
                table: "story_acknowledgments",
                column: "acknowledged_user_id");
        }
    }
}
