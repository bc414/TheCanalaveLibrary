using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class PreIntegrationCleanup_TakedownColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "moderation_removal_reason",
                table: "stories",
                newName: "takedown_reason");

            migrationBuilder.RenameColumn(
                name: "is_hidden",
                table: "stories",
                newName: "is_taken_down");

            migrationBuilder.RenameColumn(
                name: "date_moderated_removed",
                table: "stories",
                newName: "takedown_date");

            migrationBuilder.RenameColumn(
                name: "moderation_removal_reason",
                table: "recommendations",
                newName: "takedown_reason");

            migrationBuilder.RenameColumn(
                name: "is_hidden",
                table: "recommendations",
                newName: "is_taken_down");

            migrationBuilder.RenameColumn(
                name: "date_moderated_removed",
                table: "recommendations",
                newName: "takedown_date");

            migrationBuilder.RenameColumn(
                name: "moderation_removal_reason",
                table: "base_comments",
                newName: "takedown_reason");

            migrationBuilder.RenameColumn(
                name: "is_hidden",
                table: "base_comments",
                newName: "is_taken_down");

            migrationBuilder.RenameColumn(
                name: "date_moderated_removed",
                table: "base_comments",
                newName: "takedown_date");

            migrationBuilder.RenameColumn(
                name: "moderation_removal_reason",
                table: "base_blog_posts",
                newName: "takedown_reason");

            migrationBuilder.RenameColumn(
                name: "is_hidden",
                table: "base_blog_posts",
                newName: "is_taken_down");

            migrationBuilder.RenameColumn(
                name: "date_moderated_removed",
                table: "base_blog_posts",
                newName: "takedown_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "takedown_reason",
                table: "stories",
                newName: "moderation_removal_reason");

            migrationBuilder.RenameColumn(
                name: "takedown_date",
                table: "stories",
                newName: "date_moderated_removed");

            migrationBuilder.RenameColumn(
                name: "is_taken_down",
                table: "stories",
                newName: "is_hidden");

            migrationBuilder.RenameColumn(
                name: "takedown_reason",
                table: "recommendations",
                newName: "moderation_removal_reason");

            migrationBuilder.RenameColumn(
                name: "takedown_date",
                table: "recommendations",
                newName: "date_moderated_removed");

            migrationBuilder.RenameColumn(
                name: "is_taken_down",
                table: "recommendations",
                newName: "is_hidden");

            migrationBuilder.RenameColumn(
                name: "takedown_reason",
                table: "base_comments",
                newName: "moderation_removal_reason");

            migrationBuilder.RenameColumn(
                name: "takedown_date",
                table: "base_comments",
                newName: "date_moderated_removed");

            migrationBuilder.RenameColumn(
                name: "is_taken_down",
                table: "base_comments",
                newName: "is_hidden");

            migrationBuilder.RenameColumn(
                name: "takedown_reason",
                table: "base_blog_posts",
                newName: "moderation_removal_reason");

            migrationBuilder.RenameColumn(
                name: "takedown_date",
                table: "base_blog_posts",
                newName: "date_moderated_removed");

            migrationBuilder.RenameColumn(
                name: "is_taken_down",
                table: "base_blog_posts",
                newName: "is_hidden");
        }
    }
}
