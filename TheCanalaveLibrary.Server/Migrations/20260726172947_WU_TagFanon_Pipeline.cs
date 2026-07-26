using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU_TagFanon_Pipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fanon_links",
                columns: table => new
                {
                    fanon_link_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    normalized_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    base_tag_id = table.Column<int>(type: "integer", nullable: false),
                    target_tag_id = table.Column<int>(type: "integer", nullable: false),
                    linked_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    date_linked = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fanon_links", x => x.fanon_link_id);
                    table.ForeignKey(
                        name: "fk_fanon_links_tags_base_tag_id",
                        column: x => x.base_tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fanon_links_tags_target_tag_id",
                        column: x => x.target_tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_fanon_links_users_linked_by_user_id",
                        column: x => x.linked_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tag_adoption_states",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    target_tag_id = table.Column<int>(type: "integer", nullable: false),
                    date_notified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_dismissed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tag_adoption_states", x => new { x.user_id, x.target_tag_id });
                    table.ForeignKey(
                        name: "fk_tag_adoption_states_tags_target_tag_id",
                        column: x => x.target_tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tag_adoption_states_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)26,
                columns: new[] { "description", "display_name" },
                values: new object[] { "A name you used in a story matches a new official tag.", "Tag adoption suggested" });

            migrationBuilder.InsertData(
                table: "site_settings",
                columns: new[] { "setting_key", "value" },
                values: new object[] { "Fanon.MinAuthorReach", "2" });

            migrationBuilder.CreateIndex(
                name: "ix_fanon_links_base_tag_id",
                table: "fanon_links",
                column: "base_tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_fanon_links_linked_by_user_id",
                table: "fanon_links",
                column: "linked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_fanon_links_normalized_name_base_tag_id",
                table: "fanon_links",
                columns: new[] { "normalized_name", "base_tag_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fanon_links_target_tag_id",
                table: "fanon_links",
                column: "target_tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_tag_adoption_states_target_tag_id",
                table: "tag_adoption_states",
                column: "target_tag_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fanon_links");

            migrationBuilder.DropTable(
                name: "tag_adoption_states");

            migrationBuilder.DeleteData(
                table: "site_settings",
                keyColumn: "setting_key",
                keyValue: "Fanon.MinAuthorReach");

            migrationBuilder.UpdateData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)26,
                columns: new[] { "description", "display_name" },
                values: new object[] { "One of your OC tags matches a new fanon tag.", "Tag Update Suggestion" });
        }
    }
}
