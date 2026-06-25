using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU31_5b_DropPhantomBaseCommentFKs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_blog_post_comments_blog_post_comment_comment_",
                table: "base_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_chapter_comments_chapter_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_group_comments_group_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_user_profile_comments_user_profile_comment_co",
                table: "base_comments");

            migrationBuilder.DropIndex(
                name: "ix_base_comments_blog_post_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropIndex(
                name: "ix_base_comments_chapter_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropIndex(
                name: "ix_base_comments_group_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropIndex(
                name: "ix_base_comments_user_profile_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropColumn(
                name: "blog_post_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropColumn(
                name: "chapter_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropColumn(
                name: "group_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropColumn(
                name: "user_profile_comment_comment_id",
                table: "base_comments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "blog_post_comment_comment_id",
                table: "base_comments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "chapter_comment_comment_id",
                table: "base_comments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "group_comment_comment_id",
                table: "base_comments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "user_profile_comment_comment_id",
                table: "base_comments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_base_comments_blog_post_comment_comment_id",
                table: "base_comments",
                column: "blog_post_comment_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_comments_chapter_comment_comment_id",
                table: "base_comments",
                column: "chapter_comment_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_comments_group_comment_comment_id",
                table: "base_comments",
                column: "group_comment_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_comments_user_profile_comment_comment_id",
                table: "base_comments",
                column: "user_profile_comment_comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_blog_post_comments_blog_post_comment_comment_",
                table: "base_comments",
                column: "blog_post_comment_comment_id",
                principalTable: "blog_post_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_chapter_comments_chapter_comment_comment_id",
                table: "base_comments",
                column: "chapter_comment_comment_id",
                principalTable: "chapter_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_group_comments_group_comment_comment_id",
                table: "base_comments",
                column: "group_comment_comment_id",
                principalTable: "group_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_user_profile_comments_user_profile_comment_co",
                table: "base_comments",
                column: "user_profile_comment_comment_id",
                principalTable: "user_profile_comments",
                principalColumn: "comment_id");
        }
    }
}
