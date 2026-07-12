using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU_Polls_ConfigLifecycleAndShadowFkFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_polls_base_blog_posts_base_blog_post_blog_post_id",
                table: "base_polls");

            migrationBuilder.DropIndex(
                name: "ix_base_polls_base_blog_post_blog_post_id",
                table: "base_polls");

            migrationBuilder.DropColumn(
                name: "base_blog_post_blog_post_id",
                table: "base_polls");

            migrationBuilder.AddColumn<bool>(
                name: "is_anonymous",
                table: "poll_votes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_closed",
                table: "base_polls",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<bool>(
                name: "allow_multiple",
                table: "base_polls",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "anonymity_mode",
                table: "base_polls",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "edit_notified_at",
                table: "base_polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_edited_at",
                table: "base_polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "results_visibility",
                table: "base_polls",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.InsertData(
                table: "notification_types",
                columns: new[] { "notification_type_id", "default_collapsed", "default_email_enabled", "description", "display_name", "notification_category", "notification_key" },
                values: new object[] { (short)100, false, false, "A poll you voted on was changed by its owner.", "Poll Updated", (short)1, "PollUpdated" });

            migrationBuilder.CreateIndex(
                name: "ix_base_polls_last_edited_at",
                table: "base_polls",
                column: "last_edited_at",
                filter: "last_edited_at IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_base_polls_last_edited_at",
                table: "base_polls");

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)100);

            migrationBuilder.DropColumn(
                name: "is_anonymous",
                table: "poll_votes");

            migrationBuilder.DropColumn(
                name: "allow_multiple",
                table: "base_polls");

            migrationBuilder.DropColumn(
                name: "anonymity_mode",
                table: "base_polls");

            migrationBuilder.DropColumn(
                name: "edit_notified_at",
                table: "base_polls");

            migrationBuilder.DropColumn(
                name: "last_edited_at",
                table: "base_polls");

            migrationBuilder.DropColumn(
                name: "results_visibility",
                table: "base_polls");

            migrationBuilder.AlterColumn<DateTime>(
                name: "date_closed",
                table: "base_polls",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "base_blog_post_blog_post_id",
                table: "base_polls",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_base_polls_base_blog_post_blog_post_id",
                table: "base_polls",
                column: "base_blog_post_blog_post_id");

            migrationBuilder.AddForeignKey(
                name: "fk_base_polls_base_blog_posts_base_blog_post_blog_post_id",
                table: "base_polls",
                column: "base_blog_post_blog_post_id",
                principalTable: "base_blog_posts",
                principalColumn: "blog_post_id");
        }
    }
}
