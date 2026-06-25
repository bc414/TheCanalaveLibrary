using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU27_5_TagCompositeUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tags_tag_name",
                table: "tags");

            migrationBuilder.CreateIndex(
                name: "ix_tags_tag_name_tag_type_id",
                table: "tags",
                columns: new[] { "tag_name", "tag_type_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_tags_tag_name_tag_type_id",
                table: "tags");

            migrationBuilder.CreateIndex(
                name: "ix_tags_tag_name",
                table: "tags",
                column: "tag_name",
                unique: true);
        }
    }
}
