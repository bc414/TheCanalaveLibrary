using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class RenameUserStoryInteractionFilterEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. user_interaction_filters → user_story_interaction_filter_types ──────────────
            // Column: interaction_filter_key → user_story_interaction_filter_key (PK + FK target)
            // Postgres tracks FK refs by column OID; renaming the column does not break them.
            migrationBuilder.RenameColumn(
                name: "interaction_filter_key",
                table: "user_interaction_filters",
                newName: "user_story_interaction_filter_key");

            migrationBuilder.RenameTable(
                name: "user_interaction_filters",
                newName: "user_story_interaction_filter_types");

            migrationBuilder.Sql(
                "ALTER TABLE user_story_interaction_filter_types " +
                "RENAME CONSTRAINT pk_user_interaction_filters " +
                "TO pk_user_story_interaction_filter_types;");

            migrationBuilder.RenameIndex(
                name: "ix_user_interaction_filters_name",
                table: "user_story_interaction_filter_types",
                newName: "ix_user_story_interaction_filter_types_name");

            // ── 2. default_search_settings → default_user_story_interaction_filter_settings ────
            migrationBuilder.RenameColumn(
                name: "interaction_filter_key",
                table: "default_search_settings",
                newName: "user_story_interaction_filter_key");

            migrationBuilder.RenameTable(
                name: "default_search_settings",
                newName: "default_user_story_interaction_filter_settings");

            migrationBuilder.Sql(
                "ALTER TABLE default_user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT pk_default_search_settings " +
                "TO pk_default_user_story_interaction_filter_settings;");

            migrationBuilder.Sql(
                "ALTER TABLE default_user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT fk_default_search_settings_search_modes_search_mode_key " +
                "TO fk_default_user_story_interaction_filter_settings_search_modes;");

            migrationBuilder.Sql(
                "ALTER TABLE default_user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT \"fk_default_search_settings_user_interaction_filters_interactio\" " +
                "TO \"fk_default_user_story_interaction_filter_settings_user_story_i\";");

            migrationBuilder.RenameIndex(
                name: "ix_default_search_settings_interaction_filter_key",
                table: "default_user_story_interaction_filter_settings",
                newName: "ix_default_user_story_interaction_filter_settings_user_story_i");

            // ── 3. user_search_settings → user_story_interaction_filter_settings ──────────────
            migrationBuilder.RenameColumn(
                name: "user_search_setting_id",
                table: "user_search_settings",
                newName: "user_story_interaction_filter_setting_id");

            migrationBuilder.RenameColumn(
                name: "interaction_filter_key",
                table: "user_search_settings",
                newName: "user_story_interaction_filter_key");

            migrationBuilder.RenameTable(
                name: "user_search_settings",
                newName: "user_story_interaction_filter_settings");

            migrationBuilder.Sql(
                "ALTER TABLE user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT pk_user_search_settings " +
                "TO pk_user_story_interaction_filter_settings;");

            migrationBuilder.Sql(
                "ALTER TABLE user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT fk_user_search_settings_search_modes_search_mode_key " +
                "TO fk_user_story_interaction_filter_settings_search_modes_search_;");

            migrationBuilder.Sql(
                "ALTER TABLE user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT \"fk_user_search_settings_user_interaction_filters_interaction_f\" " +
                "TO \"fk_user_story_interaction_filter_settings_user_story_interacti\";");

            migrationBuilder.Sql(
                "ALTER TABLE user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT fk_user_search_settings_users_user_id " +
                "TO fk_user_story_interaction_filter_settings_users_user_id;");

            migrationBuilder.RenameIndex(
                name: "ix_user_search_settings_interaction_filter_key",
                table: "user_story_interaction_filter_settings",
                newName: "ix_user_story_interaction_filter_settings_user_story_interacti");

            migrationBuilder.RenameIndex(
                name: "ix_user_search_settings_search_mode_key",
                table: "user_story_interaction_filter_settings",
                newName: "ix_user_story_interaction_filter_settings_search_mode_key");

            migrationBuilder.RenameIndex(
                name: "ix_user_search_settings_user_id_search_mode_key_interaction_fi",
                table: "user_story_interaction_filter_settings",
                newName: "ix_user_story_interaction_filter_settings_user_id_search_mode_");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Reverse 3: user_story_interaction_filter_settings → user_search_settings ───────
            migrationBuilder.RenameIndex(
                name: "ix_user_story_interaction_filter_settings_user_id_search_mode_",
                table: "user_story_interaction_filter_settings",
                newName: "ix_user_search_settings_user_id_search_mode_key_interaction_fi");

            migrationBuilder.RenameIndex(
                name: "ix_user_story_interaction_filter_settings_search_mode_key",
                table: "user_story_interaction_filter_settings",
                newName: "ix_user_search_settings_search_mode_key");

            migrationBuilder.RenameIndex(
                name: "ix_user_story_interaction_filter_settings_user_story_interacti",
                table: "user_story_interaction_filter_settings",
                newName: "ix_user_search_settings_interaction_filter_key");

            migrationBuilder.Sql(
                "ALTER TABLE user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT fk_user_story_interaction_filter_settings_users_user_id " +
                "TO fk_user_search_settings_users_user_id;");

            migrationBuilder.Sql(
                "ALTER TABLE user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT \"fk_user_story_interaction_filter_settings_user_story_interacti\" " +
                "TO \"fk_user_search_settings_user_interaction_filters_interaction_f\";");

            migrationBuilder.Sql(
                "ALTER TABLE user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT fk_user_story_interaction_filter_settings_search_modes_search_ " +
                "TO fk_user_search_settings_search_modes_search_mode_key;");

            migrationBuilder.Sql(
                "ALTER TABLE user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT pk_user_story_interaction_filter_settings " +
                "TO pk_user_search_settings;");

            migrationBuilder.RenameTable(
                name: "user_story_interaction_filter_settings",
                newName: "user_search_settings");

            migrationBuilder.RenameColumn(
                name: "user_story_interaction_filter_key",
                table: "user_search_settings",
                newName: "interaction_filter_key");

            migrationBuilder.RenameColumn(
                name: "user_story_interaction_filter_setting_id",
                table: "user_search_settings",
                newName: "user_search_setting_id");

            // ── Reverse 2: default_user_story_interaction_filter_settings → default_search_settings
            migrationBuilder.RenameIndex(
                name: "ix_default_user_story_interaction_filter_settings_user_story_i",
                table: "default_user_story_interaction_filter_settings",
                newName: "ix_default_search_settings_interaction_filter_key");

            migrationBuilder.Sql(
                "ALTER TABLE default_user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT \"fk_default_user_story_interaction_filter_settings_user_story_i\" " +
                "TO \"fk_default_search_settings_user_interaction_filters_interactio\";");

            migrationBuilder.Sql(
                "ALTER TABLE default_user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT fk_default_user_story_interaction_filter_settings_search_modes " +
                "TO fk_default_search_settings_search_modes_search_mode_key;");

            migrationBuilder.Sql(
                "ALTER TABLE default_user_story_interaction_filter_settings " +
                "RENAME CONSTRAINT pk_default_user_story_interaction_filter_settings " +
                "TO pk_default_search_settings;");

            migrationBuilder.RenameTable(
                name: "default_user_story_interaction_filter_settings",
                newName: "default_search_settings");

            migrationBuilder.RenameColumn(
                name: "user_story_interaction_filter_key",
                table: "default_search_settings",
                newName: "interaction_filter_key");

            // ── Reverse 1: user_story_interaction_filter_types → user_interaction_filters ───────
            migrationBuilder.RenameIndex(
                name: "ix_user_story_interaction_filter_types_name",
                table: "user_story_interaction_filter_types",
                newName: "ix_user_interaction_filters_name");

            migrationBuilder.Sql(
                "ALTER TABLE user_story_interaction_filter_types " +
                "RENAME CONSTRAINT pk_user_story_interaction_filter_types " +
                "TO pk_user_interaction_filters;");

            migrationBuilder.RenameTable(
                name: "user_story_interaction_filter_types",
                newName: "user_interaction_filters");

            migrationBuilder.RenameColumn(
                name: "user_story_interaction_filter_key",
                table: "user_interaction_filters",
                newName: "interaction_filter_key");
        }
    }
}
