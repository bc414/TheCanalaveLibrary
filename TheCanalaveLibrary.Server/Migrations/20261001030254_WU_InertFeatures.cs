using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// WU-InertFeatures (2026-09-30). Two schema items, both pure EF diffs — no hand-written SQL:
    /// <list type="bullet">
    /// <item><b>Drop the phantom <c>user_story_interactions.recommendation_id</c></b> (schema audit
    /// §2.1; owner ruling D3's routing note). An unpaired <c>Recommendation.UserStoryInteractions</c>
    /// collection minted it as a shadow FK; nothing ever wrote it (the dev DB had 0 non-null rows when
    /// checked 2026-09-30). It was a fossil of the 2025 DDL's <c>SourceRecommendationID</c> column on
    /// the interaction table, from before attribution moved to <c>user_story_recommendation_sources</c>.
    /// Dropping a column keeps the table's MVCC reloptions (fillfactor/autovacuum).</item>
    /// <item><b>Widen <c>notifications.related_entity_id</c> integer → bigint</b> (schema audit §2.3;
    /// prerequisite of owner ruling D4, which anchors report outcomes on the <c>bigint</c> report id).
    /// An in-place widening rewrite: every existing value is preserved.</item>
    /// </list>
    /// <para><b>Down</b> restores both as they were. Narrowing back to integer fails if any row holds
    /// an id above <c>int.MaxValue</c> — acceptable for a rollback (no such report id exists
    /// pre-launch), and failing loudly is better than truncating.</para>
    /// </summary>
    public partial class WU_InertFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_story_interactions_recommendations_recommendation_id",
                table: "user_story_interactions");

            migrationBuilder.DropIndex(
                name: "ix_user_story_interactions_recommendation_id",
                table: "user_story_interactions");

            migrationBuilder.DropColumn(
                name: "recommendation_id",
                table: "user_story_interactions");

            migrationBuilder.AlterColumn<long>(
                name: "related_entity_id",
                table: "notifications",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "recommendation_id",
                table: "user_story_interactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "related_entity_id",
                table: "notifications",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interactions_recommendation_id",
                table: "user_story_interactions",
                column: "recommendation_id");

            migrationBuilder.AddForeignKey(
                name: "fk_user_story_interactions_recommendations_recommendation_id",
                table: "user_story_interactions",
                column: "recommendation_id",
                principalTable: "recommendations",
                principalColumn: "recommendation_id");
        }
    }
}
