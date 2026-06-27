using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU38_ThemeSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "themes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "themes",
                keyColumn: "theme_id",
                keyValue: 1,
                column: "slug",
                value: "pokemon");

            migrationBuilder.CreateIndex(
                name: "ix_themes_slug",
                table: "themes",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_themes_slug",
                table: "themes");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "themes");
        }
    }
}
