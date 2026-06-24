using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSpoilerToChapterComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_base_comments_chapter_comment_comment_id",
                table: "base_comments");

            migrationBuilder.AddColumn<bool>(
                name: "is_spoiler",
                table: "chapter_comments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_chapter_comments_chapter_comment_comment_id",
                table: "base_comments",
                column: "chapter_comment_comment_id",
                principalTable: "chapter_comments",
                principalColumn: "comment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_chapter_comments_chapter_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropColumn(
                name: "is_spoiler",
                table: "chapter_comments");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_base_comments_chapter_comment_comment_id",
                table: "base_comments",
                column: "chapter_comment_comment_id",
                principalTable: "chapter_comments",
                principalColumn: "comment_id");
        }
    }
}
