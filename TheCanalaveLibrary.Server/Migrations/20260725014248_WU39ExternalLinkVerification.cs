using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU39ExternalLinkVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "date_verification_requested",
                table: "story_external_links",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "story_external_links",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "placement_instructions",
                table: "external_platforms",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "supports_verification",
                table: "external_platforms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "verification_code",
                table: "AspNetUsers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_external_identities",
                columns: table => new
                {
                    user_external_identity_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    external_platform_id = table.Column<short>(type: "smallint", nullable: false),
                    profile_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    handle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    verification_status = table.Column<short>(type: "smallint", nullable: false),
                    date_requested = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    date_reviewed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_moderator_user_id = table.Column<int>(type: "integer", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_external_identities", x => x.user_external_identity_id);
                    table.ForeignKey(
                        name: "fk_user_external_identities_asp_net_users_reviewed_by_moderator_",
                        column: x => x.reviewed_by_moderator_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_external_identities_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_external_identities_external_platforms_external_platfo",
                        column: x => x.external_platform_id,
                        principalTable: "external_platforms",
                        principalColumn: "external_platform_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "external_platforms",
                keyColumn: "external_platform_id",
                keyValue: (short)1,
                columns: new[] { "placement_instructions", "supports_verification" },
                values: new object[] { "Add the code anywhere in your AO3 profile's \"About Me\" section (My Profile → Edit → About Me).", true });

            migrationBuilder.UpdateData(
                table: "external_platforms",
                keyColumn: "external_platform_id",
                keyValue: (short)2,
                columns: new[] { "placement_instructions", "supports_verification" },
                values: new object[] { "Add the code anywhere in your FFN profile bio, as plain text — FFN doesn't allow hyperlinks in profiles.", true });

            migrationBuilder.UpdateData(
                table: "external_platforms",
                keyColumn: "external_platform_id",
                keyValue: (short)3,
                columns: new[] { "placement_instructions", "supports_verification" },
                values: new object[] { "Add the code anywhere in your Wattpad profile bio.", true });

            migrationBuilder.UpdateData(
                table: "external_platforms",
                keyColumn: "external_platform_id",
                keyValue: (short)4,
                columns: new[] { "placement_instructions", "supports_verification" },
                values: new object[] { "Add the code to your SpaceBattles profile's \"About\" field or forum signature.", true });

            migrationBuilder.UpdateData(
                table: "external_platforms",
                keyColumn: "external_platform_id",
                keyValue: (short)5,
                columns: new[] { "placement_instructions", "supports_verification" },
                values: new object[] { "Add the code to your Sufficient Velocity profile's \"About\" field or forum signature.", true });

            migrationBuilder.UpdateData(
                table: "external_platforms",
                keyColumn: "external_platform_id",
                keyValue: (short)6,
                columns: new[] { "placement_instructions", "supports_verification" },
                values: new object[] { "Add the code anywhere in your Royal Road profile bio.", true });

            migrationBuilder.UpdateData(
                table: "external_platforms",
                keyColumn: "external_platform_id",
                keyValue: (short)7,
                columns: new[] { "placement_instructions", "supports_verification" },
                values: new object[] { null, false });

            migrationBuilder.InsertData(
                table: "notification_types",
                columns: new[] { "notification_type_id", "default_collapsed", "default_email_enabled", "description", "display_name", "notification_category", "notification_key" },
                values: new object[,]
                {
                    { (short)76, false, false, "A moderator confirmed your external platform account.", "External Account Verified", (short)3, "ExternalAccountVerified" },
                    { (short)77, false, true, "A moderator could not confirm your external platform account.", "External Account Not Verified", (short)3, "ExternalAccountRejected" },
                    { (short)78, false, false, "A moderator confirmed one of your \"also posted on\" links.", "External Link Verified", (short)2, "ExternalLinkVerified" },
                    { (short)79, false, true, "A moderator could not confirm one of your \"also posted on\" links.", "External Link Not Verified", (short)2, "ExternalLinkRejected" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_verification_code",
                table: "AspNetUsers",
                column: "verification_code",
                unique: true,
                filter: "\"verification_code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_external_identities_external_platform_id",
                table: "user_external_identities",
                column: "external_platform_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_external_identities_reviewed_by_moderator_user_id",
                table: "user_external_identities",
                column: "reviewed_by_moderator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_external_identities_user_id_external_platform_id",
                table: "user_external_identities",
                columns: new[] { "user_id", "external_platform_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_external_identities");

            migrationBuilder.DropIndex(
                name: "ix_asp_net_users_verification_code",
                table: "AspNetUsers");

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)76);

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)77);

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)78);

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)79);

            migrationBuilder.DropColumn(
                name: "date_verification_requested",
                table: "story_external_links");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "story_external_links");

            migrationBuilder.DropColumn(
                name: "placement_instructions",
                table: "external_platforms");

            migrationBuilder.DropColumn(
                name: "supports_verification",
                table: "external_platforms");

            migrationBuilder.DropColumn(
                name: "verification_code",
                table: "AspNetUsers");
        }
    }
}
