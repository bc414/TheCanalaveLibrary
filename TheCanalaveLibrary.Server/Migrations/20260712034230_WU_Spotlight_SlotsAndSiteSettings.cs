using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU_Spotlight_SlotsAndSiteSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_id",
                table: "community_spotlights");

            migrationBuilder.DropColumn(
                name: "sponsor_comment",
                table: "community_spotlights");

            migrationBuilder.AddColumn<DateTime>(
                name: "go_live_notified_utc",
                table: "community_spotlights",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "recommendation_id",
                table: "community_spotlights",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "slot_id",
                table: "community_spotlights",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "site_settings",
                columns: table => new
                {
                    setting_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_settings", x => x.setting_key);
                });

            migrationBuilder.CreateTable(
                name: "spotlight_slots",
                columns: table => new
                {
                    slot_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    granted_to_user_id = table.Column<int>(type: "integer", nullable: true),
                    granted_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    source = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    payment_id = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    granted_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_spotlight_slots", x => x.slot_id);
                    table.ForeignKey(
                        name: "fk_spotlight_slots_users_granted_by_user_id",
                        column: x => x.granted_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_spotlight_slots_users_granted_to_user_id",
                        column: x => x.granted_to_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "notification_types",
                columns: new[] { "notification_type_id", "default_collapsed", "default_email_enabled", "description", "display_name", "notification_category", "notification_key" },
                values: new object[,]
                {
                    { (short)90, false, true, "You have been awarded a Community Spotlight slot.", "Spotlight Slot Granted", (short)0, "SpotlightSlotGranted" },
                    { (short)91, false, true, "Your story is featured on the Community Spotlight.", "Story Spotlighted", (short)2, "StorySpotlighted" },
                    { (short)92, false, true, "Your recommendation is featured beside a spotlighted story.", "Recommendation Spotlighted", (short)4, "RecommendationSpotlighted" }
                });

            migrationBuilder.InsertData(
                table: "site_settings",
                columns: new[] { "setting_key", "value" },
                values: new object[,]
                {
                    { "Spotlight.BlockDurationDays", "7" },
                    { "Spotlight.BookingHorizonDays", "60" },
                    { "Spotlight.CooldownDays", "90" },
                    { "Spotlight.MonthlyGrantCap", "12" },
                    { "Spotlight.PositionCount", "3" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_community_spotlights_recommendation_id",
                table: "community_spotlights",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_spotlights_slot_id",
                table: "community_spotlights",
                column: "slot_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_spotlights_start_end",
                table: "community_spotlights",
                columns: new[] { "start_date", "end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_spotlight_slots_granted_by_user_id",
                table: "spotlight_slots",
                column: "granted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_spotlight_slots_granted_to_status",
                table: "spotlight_slots",
                columns: new[] { "granted_to_user_id", "status" });

            migrationBuilder.AddForeignKey(
                name: "fk_community_spotlights_recommendations_recommendation_id",
                table: "community_spotlights",
                column: "recommendation_id",
                principalTable: "recommendations",
                principalColumn: "recommendation_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_community_spotlights_spotlight_slots_slot_id",
                table: "community_spotlights",
                column: "slot_id",
                principalTable: "spotlight_slots",
                principalColumn: "slot_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_community_spotlights_recommendations_recommendation_id",
                table: "community_spotlights");

            migrationBuilder.DropForeignKey(
                name: "fk_community_spotlights_spotlight_slots_slot_id",
                table: "community_spotlights");

            migrationBuilder.DropTable(
                name: "site_settings");

            migrationBuilder.DropTable(
                name: "spotlight_slots");

            migrationBuilder.DropIndex(
                name: "ix_community_spotlights_recommendation_id",
                table: "community_spotlights");

            migrationBuilder.DropIndex(
                name: "ix_community_spotlights_slot_id",
                table: "community_spotlights");

            migrationBuilder.DropIndex(
                name: "ix_community_spotlights_start_end",
                table: "community_spotlights");

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)90);

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)91);

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)92);

            migrationBuilder.DropColumn(
                name: "go_live_notified_utc",
                table: "community_spotlights");

            migrationBuilder.DropColumn(
                name: "recommendation_id",
                table: "community_spotlights");

            migrationBuilder.DropColumn(
                name: "slot_id",
                table: "community_spotlights");

            migrationBuilder.AddColumn<string>(
                name: "payment_id",
                table: "community_spotlights",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sponsor_comment",
                table: "community_spotlights",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }
    }
}
