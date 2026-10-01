using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// WU-TptHardDelete (owner rulings D10 and D11, 2026-09-30).
    /// <list type="bullet">
    ///   <item>D10: the five content-parent → TPT-child FKs go CASCADE → RESTRICT (chapter →
    ///   chapter_comments, base_blog_posts → blog_post_comments and blog_post_polls, groups →
    ///   group_comments and group_blog_posts). A cascade there removed only the child row and orphaned
    ///   the base row; services now delete the base rows first via <c>TptDelete</c>.</item>
    ///   <item>D11: <c>base_polls.owner_id</c> becomes nullable with ON DELETE SET NULL (was CASCADE),
    ///   so a poll survives its owner's account deletion with its options and other users' votes.</item>
    /// </list>
    /// Up is non-destructive and every existing row already satisfies the new constraints, so no data
    /// SQL is needed. Pre-WU orphaned base rows (if a long-lived dev DB holds any) are not purged — D10
    /// declined a sweep; reset such a DB with <c>scripts/reset-dev-db.ps1</c>.
    /// </summary>
    public partial class WU_TptHardDelete_FkPosture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_polls_asp_net_users_owner_id",
                table: "base_polls");

            migrationBuilder.DropForeignKey(
                name: "fk_blog_post_comments_blog_posts_blog_post_id",
                table: "blog_post_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_blog_post_polls_base_blog_posts_blog_post_id",
                table: "blog_post_polls");

            migrationBuilder.DropForeignKey(
                name: "fk_chapter_comments_chapters_chapter_id",
                table: "chapter_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_group_blog_posts_groups_group_id",
                table: "group_blog_posts");

            migrationBuilder.DropForeignKey(
                name: "fk_group_comments_groups_group_id",
                table: "group_comments");

            migrationBuilder.AlterColumn<int>(
                name: "owner_id",
                table: "base_polls",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "fk_base_polls_asp_net_users_owner_id",
                table: "base_polls",
                column: "owner_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_blog_post_comments_blog_posts_blog_post_id",
                table: "blog_post_comments",
                column: "blog_post_id",
                principalTable: "base_blog_posts",
                principalColumn: "blog_post_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_blog_post_polls_base_blog_posts_blog_post_id",
                table: "blog_post_polls",
                column: "blog_post_id",
                principalTable: "base_blog_posts",
                principalColumn: "blog_post_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_chapter_comments_chapters_chapter_id",
                table: "chapter_comments",
                column: "chapter_id",
                principalTable: "chapters",
                principalColumn: "chapter_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_group_blog_posts_groups_group_id",
                table: "group_blog_posts",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "group_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_group_comments_groups_group_id",
                table: "group_comments",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "group_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Hand-written, first: ownerless polls would block the AlterColumn back to NOT NULL. Under the
            // old CASCADE they would have died with their owner anyway (options and votes cascade).
            migrationBuilder.Sql("DELETE FROM base_polls WHERE owner_id IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "fk_base_polls_asp_net_users_owner_id",
                table: "base_polls");

            migrationBuilder.DropForeignKey(
                name: "fk_blog_post_comments_blog_posts_blog_post_id",
                table: "blog_post_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_blog_post_polls_base_blog_posts_blog_post_id",
                table: "blog_post_polls");

            migrationBuilder.DropForeignKey(
                name: "fk_chapter_comments_chapters_chapter_id",
                table: "chapter_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_group_blog_posts_groups_group_id",
                table: "group_blog_posts");

            migrationBuilder.DropForeignKey(
                name: "fk_group_comments_groups_group_id",
                table: "group_comments");

            migrationBuilder.AlterColumn<int>(
                name: "owner_id",
                table: "base_polls",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_base_polls_asp_net_users_owner_id",
                table: "base_polls",
                column: "owner_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_blog_post_comments_blog_posts_blog_post_id",
                table: "blog_post_comments",
                column: "blog_post_id",
                principalTable: "base_blog_posts",
                principalColumn: "blog_post_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_blog_post_polls_base_blog_posts_blog_post_id",
                table: "blog_post_polls",
                column: "blog_post_id",
                principalTable: "base_blog_posts",
                principalColumn: "blog_post_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_chapter_comments_chapters_chapter_id",
                table: "chapter_comments",
                column: "chapter_id",
                principalTable: "chapters",
                principalColumn: "chapter_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_group_blog_posts_groups_group_id",
                table: "group_blog_posts",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "group_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_group_comments_groups_group_id",
                table: "group_comments",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "group_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
