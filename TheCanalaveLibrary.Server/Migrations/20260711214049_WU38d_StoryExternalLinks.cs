using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU38d_StoryExternalLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "story_imports");

            migrationBuilder.CreateTable(
                name: "external_platforms",
                columns: table => new
                {
                    external_platform_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    domain_pattern = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_platforms", x => x.external_platform_id);
                });

            migrationBuilder.CreateTable(
                name: "story_external_links",
                columns: table => new
                {
                    story_external_link_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    external_platform_id = table.Column<short>(type: "smallint", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    verification_status = table.Column<short>(type: "smallint", nullable: false),
                    date_added = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_external_links", x => x.story_external_link_id);
                    table.ForeignKey(
                        name: "fk_story_external_links_external_platforms_external_platform_id",
                        column: x => x.external_platform_id,
                        principalTable: "external_platforms",
                        principalColumn: "external_platform_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_story_external_links_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "external_platforms",
                columns: new[] { "external_platform_id", "domain_pattern", "name" },
                values: new object[,]
                {
                    { (short)1, "archiveofourown.org", "Archive of Our Own" },
                    { (short)2, "fanfiction.net", "FanFiction.Net" },
                    { (short)3, "wattpad.com", "Wattpad" },
                    { (short)4, "spacebattles.com", "SpaceBattles" },
                    { (short)5, "sufficientvelocity.com", "Sufficient Velocity" },
                    { (short)6, "royalroad.com", "Royal Road" },
                    { (short)7, null, "Other" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_platforms_name",
                table: "external_platforms",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_story_external_links_external_platform_id",
                table: "story_external_links",
                column: "external_platform_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_external_links_story_id_url",
                table: "story_external_links",
                columns: new[] { "story_id", "url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "story_external_links");

            migrationBuilder.DropTable(
                name: "external_platforms");

            migrationBuilder.CreateTable(
                name: "story_imports",
                columns: table => new
                {
                    import_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    date_imported = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    source_platform = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    verification_status = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_imports", x => x.import_id);
                    table.ForeignKey(
                        name: "fk_story_imports_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_story_imports_source_url",
                table: "story_imports",
                column: "source_url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_story_imports_story_id",
                table: "story_imports",
                column: "story_id",
                unique: true);
        }
    }
}
