using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU31_BlogPostQueryFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_base_comments_blog_post_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_blog_post_comments_base_blog_posts_blog_post_id",
                table: "blog_post_comments");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_blog_post_comments_blog_post_comment_comment_",
                table: "base_comments",
                column: "blog_post_comment_comment_id",
                principalTable: "blog_post_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_blog_post_comments_blog_posts_blog_post_id",
                table: "blog_post_comments",
                column: "blog_post_id",
                principalTable: "base_blog_posts",
                principalColumn: "blog_post_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_blog_post_comments_blog_post_comment_comment_",
                table: "base_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_blog_post_comments_blog_posts_blog_post_id",
                table: "blog_post_comments");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_base_comments_blog_post_comment_comment_id",
                table: "base_comments",
                column: "blog_post_comment_comment_id",
                principalTable: "blog_post_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_blog_post_comments_base_blog_posts_blog_post_id",
                table: "blog_post_comments",
                column: "blog_post_id",
                principalTable: "base_blog_posts",
                principalColumn: "blog_post_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
