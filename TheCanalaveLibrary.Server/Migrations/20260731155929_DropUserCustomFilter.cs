using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class DropUserCustomFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_custom_filters");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_custom_filters",
                columns: table => new
                {
                    user_custom_filter_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    search_mode_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    filter_entity_type = table.Column<short>(type: "smallint", nullable: false),
                    include = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_custom_filters", x => x.user_custom_filter_id);
                    table.ForeignKey(
                        name: "fk_user_custom_filters_search_modes_search_mode_key",
                        column: x => x.search_mode_key,
                        principalTable: "search_modes",
                        principalColumn: "search_mode_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_custom_filters_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_custom_filters_search_mode_key",
                table: "user_custom_filters",
                column: "search_mode_key");

            migrationBuilder.CreateIndex(
                name: "ix_user_custom_filters_user_id",
                table: "user_custom_filters",
                column: "user_id");
        }
    }
}
