using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    // Hand-edited after scaffolding (WU42 — Story Lineage feature-wide rename): the naive
    // scaffold emitted DropTable+CreateTable for both tables, which would have discarded any
    // dev-environment rows. Rewritten as RenameTable + constraint/index renames — no data loss.
    // See audit/Stories.md Feature 10 and CLAUDE.md "Doc-Touch Timing" Phase 0 note.
    public partial class AddStoryLineageRename : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "story_relationships",
                newName: "story_lineages");

            migrationBuilder.RenameTable(
                name: "story_relationship_types",
                newName: "story_lineage_types");

            migrationBuilder.Sql(
                "ALTER TABLE story_lineage_types RENAME CONSTRAINT pk_story_relationship_types TO pk_story_lineage_types;");
            migrationBuilder.Sql(
                "ALTER TABLE story_lineages RENAME CONSTRAINT pk_story_relationships TO pk_story_lineages;");
            migrationBuilder.Sql(
                "ALTER TABLE story_lineages RENAME CONSTRAINT fk_story_relationships_stories_source_story_id TO fk_story_lineages_stories_source_story_id;");
            migrationBuilder.Sql(
                "ALTER TABLE story_lineages RENAME CONSTRAINT fk_story_relationships_stories_target_story_id TO fk_story_lineages_stories_target_story_id;");
            migrationBuilder.Sql(
                "ALTER TABLE story_lineages RENAME CONSTRAINT fk_story_relationships_story_relationship_types_relationship_t TO fk_story_lineages_story_lineage_types_relationship_type_id;");

            migrationBuilder.RenameIndex(
                table: "story_lineages",
                name: "ix_story_relationships_relationship_type_id",
                newName: "ix_story_lineages_relationship_type_id");

            migrationBuilder.RenameIndex(
                table: "story_lineages",
                name: "ix_story_relationships_target_story_id",
                newName: "ix_story_lineages_target_story_id");

            migrationBuilder.UpdateData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)50,
                columns: new[] { "display_name", "notification_key" },
                values: new object[] { "New Story Lineage Request", "StoryLineageRequested" });

            migrationBuilder.UpdateData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)51,
                columns: new[] { "display_name", "notification_key" },
                values: new object[] { "Story Lineage Approved", "StoryLineageApproved" });

            // No InsertData here — the four seeded type rows (Inspired By/Prequel/Sequel/
            // Companion Piece) already exist under the renamed table; RenameTable preserves them.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)50,
                columns: new[] { "display_name", "notification_key" },
                values: new object[] { "New Story Relationship Request", "StoryRelationshipRequested" });

            migrationBuilder.UpdateData(
                table: "notification_types",
                keyColumn: "notification_type_id",
                keyValue: (short)51,
                columns: new[] { "display_name", "notification_key" },
                values: new object[] { "Story Relationship Approved", "StoryRelationshipApproved" });

            migrationBuilder.RenameIndex(
                table: "story_lineages",
                name: "ix_story_lineages_relationship_type_id",
                newName: "ix_story_relationships_relationship_type_id");

            migrationBuilder.RenameIndex(
                table: "story_lineages",
                name: "ix_story_lineages_target_story_id",
                newName: "ix_story_relationships_target_story_id");

            migrationBuilder.Sql(
                "ALTER TABLE story_lineages RENAME CONSTRAINT fk_story_lineages_story_lineage_types_relationship_type_id TO fk_story_relationships_story_relationship_types_relationship_t;");
            migrationBuilder.Sql(
                "ALTER TABLE story_lineages RENAME CONSTRAINT fk_story_lineages_stories_target_story_id TO fk_story_relationships_stories_target_story_id;");
            migrationBuilder.Sql(
                "ALTER TABLE story_lineages RENAME CONSTRAINT fk_story_lineages_stories_source_story_id TO fk_story_relationships_stories_source_story_id;");
            migrationBuilder.Sql(
                "ALTER TABLE story_lineages RENAME CONSTRAINT pk_story_lineages TO pk_story_relationships;");
            migrationBuilder.Sql(
                "ALTER TABLE story_lineage_types RENAME CONSTRAINT pk_story_lineage_types TO pk_story_relationship_types;");

            migrationBuilder.RenameTable(
                name: "story_lineage_types",
                newName: "story_relationship_types");

            migrationBuilder.RenameTable(
                name: "story_lineages",
                newName: "story_relationships");
        }
    }
}
