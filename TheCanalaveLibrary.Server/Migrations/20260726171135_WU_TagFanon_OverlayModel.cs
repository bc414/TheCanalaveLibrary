using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU_TagFanon_OverlayModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── WU-TagFanon data-preserving overlay-model migration (plan 2.10: existing data
            // must survive — no transformation, no loss). Hand-reordered from the scaffold. ──

            // The two gate flags merge into one: allow_custom_name = allow_setting_details OR
            // allow_oc_details. Fold BEFORE dropping the OC flag and renaming the survivor.
            migrationBuilder.Sql(
                "UPDATE tags SET allow_setting_details = allow_setting_details OR allow_oc_details;");

            migrationBuilder.DropColumn(
                name: "allow_oc_details",
                table: "tags");

            migrationBuilder.RenameColumn(
                name: "allow_setting_details",
                table: "tags",
                newName: "allow_custom_name");

            migrationBuilder.RenameColumn(
                name: "oc_name",
                table: "story_characters",
                newName: "custom_name");

            migrationBuilder.RenameColumn(
                name: "oc_bio",
                table: "story_characters",
                newName: "nuance");

            // Guard the 512→500 shrink — spec-drift correction (H6); no seeded description
            // exceeds 500, but a hand-entered one must not fail the ALTER.
            migrationBuilder.Sql(
                "UPDATE tags SET description = LEFT(description, 500) WHERE LENGTH(description) > 500;");

            migrationBuilder.AlterColumn<string>(
                name: "sprite_identifier",
                table: "tags",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "tags",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "custom_name",
                table: "story_tags",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "nuance",
                table: "story_tags",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            // Fold the SettingDetail side-rows onto their StoryTag junction rows (0-or-1 per
            // (story, tag) by the old unique index, so this is a straight move), THEN drop the table.
            migrationBuilder.Sql("""
                UPDATE story_tags st
                SET custom_name = LEFT(sd.name, 128), nuance = sd.description
                FROM setting_details sd
                WHERE sd.story_id = st.story_id AND sd.base_tag_id = st.tag_id;
                """);

            migrationBuilder.DropTable(
                name: "setting_details");

            migrationBuilder.DropIndex(
                name: "ix_story_characters_story_id",
                table: "story_characters");

            migrationBuilder.CreateIndex(
                name: "ix_story_characters_story_id_character_tag_id_custom_name",
                table: "story_characters",
                columns: new[] { "story_id", "character_tag_id", "custom_name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_story_characters_story_id_character_tag_id_custom_name",
                table: "story_characters");

            migrationBuilder.DropColumn(
                name: "custom_name",
                table: "story_tags");

            migrationBuilder.DropColumn(
                name: "nuance",
                table: "story_tags");

            migrationBuilder.RenameColumn(
                name: "allow_custom_name",
                table: "tags",
                newName: "allow_setting_details");

            migrationBuilder.RenameColumn(
                name: "nuance",
                table: "story_characters",
                newName: "oc_bio");

            migrationBuilder.RenameColumn(
                name: "custom_name",
                table: "story_characters",
                newName: "oc_name");

            migrationBuilder.AlterColumn<string>(
                name: "sprite_identifier",
                table: "tags",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "description",
                table: "tags",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_oc_details",
                table: "tags",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "setting_details",
                columns: table => new
                {
                    setting_detail_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    base_tag_id = table.Column<int>(type: "integer", nullable: false),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_setting_details", x => x.setting_detail_id);
                    table.ForeignKey(
                        name: "fk_setting_details_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_setting_details_tags_base_tag_id",
                        column: x => x.base_tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_story_characters_story_id",
                table: "story_characters",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_setting_details_base_tag_id",
                table: "setting_details",
                column: "base_tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_setting_details_story_id_base_tag_id",
                table: "setting_details",
                columns: new[] { "story_id", "base_tag_id" },
                unique: true);
        }
    }
}
