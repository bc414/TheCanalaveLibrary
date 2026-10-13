using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// Owner ruling D19 (WU-NotificationCorrectness): <c>user_notification_settings</c> becomes sparse
    /// <b>per field</b>. Both override columns turn nullable (NULL = follow the type's default for that
    /// field), and existing rows are canonicalized: a value equal to its type default becomes NULL,
    /// and a row left with both columns NULL is deleted ("no row" is the all-default state).
    ///
    /// <para>The UPDATE/DELETE statements are one-shot data work, not schema, so they do not join
    /// <c>layer1-data-model.md</c>'s manual-DDL re-append registry: a future InitialSchema collapse runs
    /// on an empty database. <c>Down()</c> backfills the NULLs with the type defaults before restoring
    /// NOT NULL, which would otherwise fail on them.</para>
    /// </summary>
    public partial class WU_NotificationCorrectness_SparseSettingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "email_enabled",
                table: "user_notification_settings",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "collapsed",
                table: "user_notification_settings",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            // Canonicalize the existing rows to the per-field rule, so they track the defaults.
            migrationBuilder.Sql("""
                UPDATE user_notification_settings s SET email_enabled = NULL
                  FROM notification_types t
                 WHERE t.notification_type_id = s.notification_type_id
                   AND s.email_enabled = t.default_email_enabled;
                UPDATE user_notification_settings s SET collapsed = NULL
                  FROM notification_types t
                 WHERE t.notification_type_id = s.notification_type_id
                   AND s.collapsed = t.default_collapsed;
                DELETE FROM user_notification_settings
                 WHERE email_enabled IS NULL AND collapsed IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to row-level values: a NULL field means "the type default", so write that default
            // in before NOT NULL comes back (the AlterColumns below would fail on the NULLs).
            migrationBuilder.Sql("""
                UPDATE user_notification_settings s
                   SET email_enabled = COALESCE(s.email_enabled, t.default_email_enabled),
                       collapsed = COALESCE(s.collapsed, t.default_collapsed)
                  FROM notification_types t
                 WHERE t.notification_type_id = s.notification_type_id;
                """);

            migrationBuilder.AlterColumn<bool>(
                name: "email_enabled",
                table: "user_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "collapsed",
                table: "user_notification_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);
        }
    }
}
