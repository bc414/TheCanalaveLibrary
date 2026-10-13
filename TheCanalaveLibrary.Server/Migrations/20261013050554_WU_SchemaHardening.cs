using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU_SchemaHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-written (WU-SchemaHardening): normalize rows the new constraints would refuse, BEFORE
            // the scaffolded operations add them. One-off DML for databases that already hold data, not
            // re-append DDL (layer1-data-model.md §"CHECK constraints"): a fresh database has no rows,
            // and every statement is a no-op on DataSeeder and SeedTool data (checked on the dev DB,
            // 2026-10-13). Not reversed in Down(): the old schema accepts the normalized rows.
            migrationBuilder.Sql("""
                -- Self-FK: a parent that is the folder itself, missing, or in another group -> top level.
                -- Pre-FK deletes left such sub-folders pointing at nothing, invisible in the tree.
                UPDATE group_folders c SET parent_folder_id = NULL
                 WHERE c.parent_folder_id IS NOT NULL
                   AND (c.parent_folder_id = c.group_folder_id
                        OR NOT EXISTS (SELECT 1 FROM group_folders p
                                        WHERE p.group_folder_id = c.parent_folder_id AND p.group_id = c.group_id));

                -- NULLS NOT DISTINCT: duplicate top-level names (including those just promoted). The lowest
                -- id keeps its name; the others get " (<id>)", inside varchar(100) for any int id.
                UPDATE group_folders f SET name = left(f.name, 85) || ' (' || f.group_folder_id || ')'
                 WHERE f.parent_folder_id IS NULL
                   AND EXISTS (SELECT 1 FROM group_folders o
                                WHERE o.group_id = f.group_id AND o.parent_folder_id IS NULL
                                  AND o.name = f.name AND o.group_folder_id < f.group_folder_id);

                -- group_stories is a set (D23): keep the lowest id; a duplicate's folder assignments cascade.
                DELETE FROM group_stories d USING group_stories k
                 WHERE d.group_id = k.group_id AND d.story_id = k.story_id AND d.group_story_id > k.group_story_id;

                -- ck_base_polls_dates and ck_users_suspension.
                UPDATE base_polls SET date_closed = date_opened WHERE date_closed < date_opened;
                UPDATE "AspNetUsers" SET suspended_until_utc = NULL
                 WHERE account_status <> 2 AND suspended_until_utc IS NOT NULL;

                -- new_users NOT NULL (the aggregator has always written a count; this is defensive).
                UPDATE site_daily_stats SET new_users = 0 WHERE new_users IS NULL;
                """);

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_story_interaction_filter_settings",
                table: "user_story_interaction_filter_settings");

            migrationBuilder.DropIndex(
                name: "ix_user_story_interaction_filter_settings_user_id_search_mode_",
                table: "user_story_interaction_filter_settings");

            migrationBuilder.DropIndex(
                name: "ix_group_stories_group_id",
                table: "group_stories");

            migrationBuilder.DropIndex(
                name: "ix_group_folders_group_id_parent_folder_id_name",
                table: "group_folders");

            migrationBuilder.DropColumn(
                name: "user_story_interaction_filter_setting_id",
                table: "user_story_interaction_filter_settings");

            migrationBuilder.AlterColumn<int>(
                name: "new_users",
                table: "site_daily_stats",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_story_interaction_filter_settings",
                table: "user_story_interaction_filter_settings",
                columns: new[] { "user_id", "search_mode_key", "user_story_interaction_filter_key" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_vouches_no_self",
                table: "vouches",
                sql: "vouching_user_id <> vouched_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_story_lineages_no_self",
                table: "story_lineages",
                sql: "source_story_id <> target_story_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_story_arcs_bounds",
                table: "story_arcs",
                sql: "start_chapter_number <= end_chapter_number");

            migrationBuilder.AddCheckConstraint(
                name: "ck_stories_published_date",
                table: "stories",
                sql: "story_status_id NOT BETWEEN 2 AND 7 OR published_date IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_group_stories_group_id_story_id",
                table: "group_stories",
                columns: new[] { "group_id", "story_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_folders_group_id_parent_folder_id_name",
                table: "group_folders",
                columns: new[] { "group_id", "parent_folder_id", "name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_group_folders_parent_folder_id",
                table: "group_folders",
                column: "parent_folder_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_folders_no_self",
                table: "group_folders",
                sql: "parent_folder_id IS NULL OR parent_folder_id <> group_folder_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_followed_users_no_self",
                table: "followed_users",
                sql: "user_id <> followed_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fanon_links_no_self",
                table: "fanon_links",
                sql: "base_tag_id <> target_tag_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_chapters_first_published_date",
                table: "chapters",
                sql: "NOT is_published OR first_published_date IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_base_polls_dates",
                table: "base_polls",
                sql: "date_closed IS NULL OR date_closed >= date_opened");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_suspension",
                table: "AspNetUsers",
                sql: "account_status = 2 OR suspended_until_utc IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_group_folders_group_folders_parent_folder_id",
                table: "group_folders",
                column: "parent_folder_id",
                principalTable: "group_folders",
                principalColumn: "group_folder_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_group_folders_group_folders_parent_folder_id",
                table: "group_folders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_vouches_no_self",
                table: "vouches");

            migrationBuilder.DropPrimaryKey(
                name: "pk_user_story_interaction_filter_settings",
                table: "user_story_interaction_filter_settings");

            migrationBuilder.DropCheckConstraint(
                name: "ck_story_lineages_no_self",
                table: "story_lineages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_story_arcs_bounds",
                table: "story_arcs");

            migrationBuilder.DropCheckConstraint(
                name: "ck_stories_published_date",
                table: "stories");

            migrationBuilder.DropIndex(
                name: "ix_group_stories_group_id_story_id",
                table: "group_stories");

            migrationBuilder.DropIndex(
                name: "ix_group_folders_group_id_parent_folder_id_name",
                table: "group_folders");

            migrationBuilder.DropIndex(
                name: "ix_group_folders_parent_folder_id",
                table: "group_folders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_group_folders_no_self",
                table: "group_folders");

            migrationBuilder.DropCheckConstraint(
                name: "ck_followed_users_no_self",
                table: "followed_users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fanon_links_no_self",
                table: "fanon_links");

            migrationBuilder.DropCheckConstraint(
                name: "ck_chapters_first_published_date",
                table: "chapters");

            migrationBuilder.DropCheckConstraint(
                name: "ck_base_polls_dates",
                table: "base_polls");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_suspension",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "user_story_interaction_filter_setting_id",
                table: "user_story_interaction_filter_settings",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "new_users",
                table: "site_daily_stats",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddPrimaryKey(
                name: "pk_user_story_interaction_filter_settings",
                table: "user_story_interaction_filter_settings",
                column: "user_story_interaction_filter_setting_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interaction_filter_settings_user_id_search_mode_",
                table: "user_story_interaction_filter_settings",
                columns: new[] { "user_id", "search_mode_key", "user_story_interaction_filter_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_stories_group_id",
                table: "group_stories",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_folders_group_id_parent_folder_id_name",
                table: "group_folders",
                columns: new[] { "group_id", "parent_folder_id", "name" },
                unique: true);
        }
    }
}
