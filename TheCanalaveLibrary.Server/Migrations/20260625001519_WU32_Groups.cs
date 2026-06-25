using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU32_Groups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_base_comments_group_comment_comment_id",
                table: "base_comments");

            migrationBuilder.RenameColumn(
                name: "rating",
                table: "groups",
                newName: "audience_rating");

            migrationBuilder.AddColumn<bool>(
                name: "has_spoilers",
                table: "group_blog_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "story_id",
                table: "group_blog_posts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_group_comments_group_comment_comment_id",
                table: "base_comments",
                column: "group_comment_comment_id",
                principalTable: "group_comments",
                principalColumn: "comment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_group_comments_group_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropColumn(
                name: "has_spoilers",
                table: "group_blog_posts");

            migrationBuilder.DropColumn(
                name: "story_id",
                table: "group_blog_posts");

            migrationBuilder.RenameColumn(
                name: "audience_rating",
                table: "groups",
                newName: "rating");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_base_comments_group_comment_comment_id",
                table: "base_comments",
                column: "group_comment_comment_id",
                principalTable: "group_comments",
                principalColumn: "comment_id");
        }
    }
}
