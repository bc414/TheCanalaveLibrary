using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class RecLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "recommendation_statuses",
                keyColumn: "recommendation_status_id",
                keyValue: (short)4);

            migrationBuilder.AddColumn<string>(
                name: "revision_request_note",
                table: "recommendations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.InsertData(
                table: "notification_types",
                columns: new[] { "notification_type_id", "default_collapsed", "default_email_enabled", "description", "display_name", "notification_category", "notification_key" },
                values: new object[,]
                {
                    { (short)27, false, true, "A recommendation you sent back for revision was edited and is live again.", "Recommendation Revised", (short)2, "RecommendationRevised" },
                    { (short)43, false, true, "An author asked you to revise your recommendation.", "Revision Requested", (short)4, "RecommendationRevisionRequested" }
                });

            migrationBuilder.UpdateData(
                table: "recommendation_statuses",
                keyColumn: "recommendation_status_id",
                keyValue: (short)1,
                columns: new[] { "description", "status_name" },
                values: new object[] { "The story author requested a revision; hidden until the recommender edits it.", "Needs Revision" });

            migrationBuilder.UpdateData(
                table: "recommendation_statuses",
                keyColumn: "recommendation_status_id",
                keyValue: (short)3,
                column: "description",
                value: "Removed by the story author, not visible; blocked until the author unblocks it.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)27);

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)43);

            migrationBuilder.DropColumn(
                name: "revision_request_note",
                table: "recommendations");

            migrationBuilder.UpdateData(
                table: "recommendation_statuses",
                keyColumn: "recommendation_status_id",
                keyValue: (short)1,
                columns: new[] { "description", "status_name" },
                values: new object[] { "Submitted by user, awaiting author review.", "Pending Approval" });

            migrationBuilder.UpdateData(
                table: "recommendation_statuses",
                keyColumn: "recommendation_status_id",
                keyValue: (short)3,
                column: "description",
                value: "Rejected by author, not visible.");

            migrationBuilder.InsertData(
                table: "recommendation_statuses",
                columns: new[] { "recommendation_status_id", "description", "status_name" },
                values: new object[] { (short)4, "An approved recommendation that was reported and is under review.", "Under Review" });
        }
    }
}
