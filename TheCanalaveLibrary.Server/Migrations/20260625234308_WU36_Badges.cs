using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU36_Badges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "recommendation_successes_earned",
                table: "user_stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "Architect",
                column: "description",
                value: "Helped develop a site feature.");

            migrationBuilder.UpdateData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "Artist",
                column: "description",
                value: "Created cover art for others' stories.");

            migrationBuilder.UpdateData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "BetaReader",
                column: "description",
                value: "Acknowledged as a beta reader on others' stories.");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "RecommenderSilver");

            migrationBuilder.DropColumn(
                name: "recommendation_successes_earned",
                table: "user_stats");

            migrationBuilder.UpdateData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "Architect",
                column: "description",
                value: "Helped develop a site feature");

            migrationBuilder.UpdateData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "Artist",
                column: "description",
                value: "Made cover art for others");

            migrationBuilder.UpdateData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "BetaReader",
                column: "description",
                value: "Acknowledged as a Beta Reader on stories.");

            migrationBuilder.UpdateData(
                table: "badges",
                keyColumn: "badge_key",
                keyValue: "Recommender",
                column: "description",
                value: "Has many successful recs");
        }
    }
}
