using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU30_UserProfileCommentDbSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_base_comments_user_profile_comment_comment_id",
                table: "base_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_user_profile_comments_asp_net_users_profile_user_id",
                table: "user_profile_comments");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_user_profile_comments_user_profile_comment_co",
                table: "base_comments",
                column: "user_profile_comment_comment_id",
                principalTable: "user_profile_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_user_profile_comments_users_profile_user_id",
                table: "user_profile_comments",
                column: "profile_user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_user_profile_comments_user_profile_comment_co",
                table: "base_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_user_profile_comments_users_profile_user_id",
                table: "user_profile_comments");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_base_comments_user_profile_comment_comment_id",
                table: "base_comments",
                column: "user_profile_comment_comment_id",
                principalTable: "user_profile_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_user_profile_comments_asp_net_users_profile_user_id",
                table: "user_profile_comments",
                column: "profile_user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
