using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU34_Moderation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "date_moderated_removed",
                table: "stories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "stories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "moderation_removal_reason",
                table: "stories",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "reported_entity_id",
                table: "reports",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "date_moderated_removed",
                table: "recommendations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "recommendations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "moderation_removal_reason",
                table: "recommendations",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "date_moderated_removed",
                table: "base_comments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "base_comments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "moderation_removal_reason",
                table: "base_comments",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "date_moderated_removed",
                table: "base_blog_posts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_hidden",
                table: "base_blog_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "moderation_removal_reason",
                table: "base_blog_posts",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "account_status",
                table: "AspNetUsers",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<int>(
                name: "active_report_count",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "suspended_until_utc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.InsertData(
                table: "notification_types",
                columns: new[] { "notification_type_id", "default_collapsed", "default_email_enabled", "description", "display_name", "notification_category", "notification_key" },
                values: new object[] { (short)75, false, true, "Your story submission was approved.", "Story Approved", (short)2, "StoryApproved" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_reported_entity_type_reported_entity_id",
                table: "reports",
                columns: new[] { "reported_entity_type", "reported_entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reports_reported_entity_type_reported_entity_id",
                table: "reports");

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)75);

            migrationBuilder.DropColumn(
                name: "date_moderated_removed",
                table: "stories");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "stories");

            migrationBuilder.DropColumn(
                name: "moderation_removal_reason",
                table: "stories");

            migrationBuilder.DropColumn(
                name: "date_moderated_removed",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "moderation_removal_reason",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "date_moderated_removed",
                table: "base_comments");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "base_comments");

            migrationBuilder.DropColumn(
                name: "moderation_removal_reason",
                table: "base_comments");

            migrationBuilder.DropColumn(
                name: "date_moderated_removed",
                table: "base_blog_posts");

            migrationBuilder.DropColumn(
                name: "is_hidden",
                table: "base_blog_posts");

            migrationBuilder.DropColumn(
                name: "moderation_removal_reason",
                table: "base_blog_posts");

            migrationBuilder.DropColumn(
                name: "account_status",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "active_report_count",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "suspended_until_utc",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<int>(
                name: "reported_entity_id",
                table: "reports",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
