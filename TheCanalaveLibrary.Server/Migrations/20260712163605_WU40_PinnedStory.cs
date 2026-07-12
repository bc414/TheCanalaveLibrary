using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU40_PinnedStory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_stories_users_author_id",
                table: "stories");

            migrationBuilder.AddColumn<int>(
                name: "pinned_story_id",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_pinned_story_id",
                table: "AspNetUsers",
                column: "pinned_story_id");

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_users_stories_pinned_story_id",
                table: "AspNetUsers",
                column: "pinned_story_id",
                principalTable: "stories",
                principalColumn: "story_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_stories_asp_net_users_author_id",
                table: "stories",
                column: "author_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_users_stories_pinned_story_id",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "fk_stories_asp_net_users_author_id",
                table: "stories");

            migrationBuilder.DropIndex(
                name: "ix_asp_net_users_pinned_story_id",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "pinned_story_id",
                table: "AspNetUsers");

            migrationBuilder.AddForeignKey(
                name: "fk_stories_users_author_id",
                table: "stories",
                column: "author_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
