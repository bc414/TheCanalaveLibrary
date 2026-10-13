using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// Owner ruling D13 edge (b) (WU-UserDeletion, 2026-10-13): seeds notification type 62,
    /// <c>GroupAdminAssigned</c> (Groups category, email on by default). <c>ReportedEntityType.Group = 6</c>
    /// needs no migration: no lookup table backs that enum. Down removes the type's rows first, since
    /// both referencing FKs are RESTRICT.
    /// </summary>
    public partial class WU_UserDeletion_GroupAdminAssigned : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "notification_types",
                columns: new[] { "notification_type_id", "default_collapsed", "default_email_enabled", "description", "display_name", "notification_category", "notification_key" },
                values: new object[] { (short)62, false, true, "A site moderator made you an admin of a group that had none.", "Group Admin Assigned", (short)6, "GroupAdminAssigned" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // notifications and user_notification_settings reference the type with ON DELETE RESTRICT.
            migrationBuilder.Sql("DELETE FROM notifications WHERE notification_type_id = 62;");
            migrationBuilder.Sql("DELETE FROM user_notification_settings WHERE notification_type_id = 62;");

            migrationBuilder.DeleteData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)62);
        }
    }
}
