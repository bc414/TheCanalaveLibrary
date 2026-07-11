using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU43_SavedTagSelectionExcludeAndDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "saved_tag_selections",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_excluded",
                table: "saved_tag_selection_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_saved_tag_selections_user_id_date_created",
                table: "saved_tag_selections",
                columns: new[] { "user_id", "date_created" });

            migrationBuilder.CreateIndex(
                name: "ix_saved_tag_selections_user_id_is_public",
                table: "saved_tag_selections",
                columns: new[] { "user_id", "is_public" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_saved_tag_selections_user_id_date_created",
                table: "saved_tag_selections");

            migrationBuilder.DropIndex(
                name: "ix_saved_tag_selections_user_id_is_public",
                table: "saved_tag_selections");

            migrationBuilder.DropColumn(
                name: "description",
                table: "saved_tag_selections");

            migrationBuilder.DropColumn(
                name: "is_excluded",
                table: "saved_tag_selection_entries");
        }
    }
}
