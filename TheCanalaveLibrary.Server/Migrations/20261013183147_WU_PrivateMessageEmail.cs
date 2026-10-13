using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// Owner ruling D18 (WU-PrivateMessageEmail, 2026-10-13): private-message email's whole schema
    /// footprint, two columns — no enum member, category, seed row or index.
    /// <list type="bullet">
    ///   <item><c>AspNetUsers.email_on_new_message</c>: NOT NULL DEFAULT true, from the model's
    ///   <c>HasDefaultValue(true).HasSentinel(true)</c> (layer1-data-model.md, the bool-true shape). The
    ///   default backfills every existing user on in this one statement, matching what a new user
    ///   gets.</item>
    ///   <item><c>conversation_participants.last_notified_timestamp</c>: nullable, no backfill. NULL
    ///   means "this unread run has not been mailed", so an existing conversation with unread messages
    ///   mails once, on its next new message — which is the rule, not a migration artefact.</item>
    /// </list>
    /// No data to preserve on Down: both columns are new and nothing else reads them.
    /// </summary>
    public partial class WU_PrivateMessageEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_notified_timestamp",
                table: "conversation_participants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "email_on_new_message",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_notified_timestamp",
                table: "conversation_participants");

            migrationBuilder.DropColumn(
                name: "email_on_new_message",
                table: "AspNetUsers");
        }
    }
}
