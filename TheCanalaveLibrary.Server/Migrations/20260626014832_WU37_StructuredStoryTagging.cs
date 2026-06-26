using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU37_StructuredStoryTagging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "story_character_story_character_relationship");

            migrationBuilder.DropTable(
                name: "story_character_relationships");

            migrationBuilder.DropIndex(
                name: "ix_setting_details_story_id",
                table: "setting_details");

            migrationBuilder.DeleteData(
                table: "tag_types",
                keyColumn: "tag_type_id",
                keyValue: (short)5);

            migrationBuilder.AddColumn<bool>(
                name: "allow_setting_details",
                table: "tags",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "story_character_pairings",
                columns: table => new
                {
                    story_character_pairing_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    pairing_type = table.Column<short>(type: "smallint", nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_character_pairings", x => x.story_character_pairing_id);
                    table.ForeignKey(
                        name: "fk_story_character_pairings_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_character_pairing_members",
                columns: table => new
                {
                    story_character_pairing_id = table.Column<int>(type: "integer", nullable: false),
                    story_character_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_character_pairing_members", x => new { x.story_character_pairing_id, x.story_character_id });
                    table.ForeignKey(
                        name: "fk_story_character_pairing_members_story_character_pairings_st",
                        column: x => x.story_character_pairing_id,
                        principalTable: "story_character_pairings",
                        principalColumn: "story_character_pairing_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_story_character_pairing_members_story_characters_story_char",
                        column: x => x.story_character_id,
                        principalTable: "story_characters",
                        principalColumn: "story_character_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_setting_details_story_id_base_tag_id",
                table: "setting_details",
                columns: new[] { "story_id", "base_tag_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_story_character_pairing_members_story_character_id",
                table: "story_character_pairing_members",
                column: "story_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_character_pairings_story_id",
                table: "story_character_pairings",
                column: "story_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "story_character_pairing_members");

            migrationBuilder.DropTable(
                name: "story_character_pairings");

            migrationBuilder.DropIndex(
                name: "ix_setting_details_story_id_base_tag_id",
                table: "setting_details");

            migrationBuilder.DropColumn(
                name: "allow_setting_details",
                table: "tags");

            migrationBuilder.CreateTable(
                name: "story_character_relationships",
                columns: table => new
                {
                    story_character_relationship_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false),
                    relationship_type = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_character_relationships", x => x.story_character_relationship_id);
                    table.ForeignKey(
                        name: "fk_story_character_relationships_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_character_story_character_relationship",
                columns: table => new
                {
                    story_character_relationships_story_character_relationship_id = table.Column<int>(type: "integer", nullable: false),
                    story_characters_story_character_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_character_story_character_relationship", x => new { x.story_character_relationships_story_character_relationship_id, x.story_characters_story_character_id });
                    table.ForeignKey(
                        name: "fk_story_character_story_character_relationship_story_characte",
                        column: x => x.story_character_relationships_story_character_relationship_id,
                        principalTable: "story_character_relationships",
                        principalColumn: "story_character_relationship_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_story_character_story_character_relationship_story_characte1",
                        column: x => x.story_characters_story_character_id,
                        principalTable: "story_characters",
                        principalColumn: "story_character_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "tag_types",
                columns: new[] { "tag_type_id", "type_name" },
                values: new object[] { (short)5, "Relationship" });

            migrationBuilder.CreateIndex(
                name: "ix_setting_details_story_id",
                table: "setting_details",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_character_relationships_story_id",
                table: "story_character_relationships",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_character_story_character_relationship_story_characte",
                table: "story_character_story_character_relationship",
                column: "story_characters_story_character_id");
        }
    }
}
