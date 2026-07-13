using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU45_StoryArcDropSortOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_story_arcs_story_id_sort_order",
                table: "story_arcs");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "story_arcs");

            migrationBuilder.CreateIndex(
                name: "ix_story_arcs_story_id_start_chapter_number",
                table: "story_arcs",
                columns: new[] { "story_id", "start_chapter_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_story_arcs_story_id_start_chapter_number",
                table: "story_arcs");

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "story_arcs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_story_arcs_story_id_sort_order",
                table: "story_arcs",
                columns: new[] { "story_id", "sort_order" },
                unique: true);
        }
    }
}
