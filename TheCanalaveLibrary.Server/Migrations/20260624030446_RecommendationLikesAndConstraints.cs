using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationLikesAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_recommendations_recommender_id",
                table: "recommendations");

            migrationBuilder.AddColumn<int>(
                name: "like_count",
                table: "recommendations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "recommendation_likes",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    recommendation_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_likes", x => new { x.user_id, x.recommendation_id });
                    table.ForeignKey(
                        name: "fk_recommendation_likes_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "recommendation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recommendation_likes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_recommender_id_story_id",
                table: "recommendations",
                columns: new[] { "recommender_id", "story_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_likes_recommendation_id",
                table: "recommendation_likes",
                column: "recommendation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recommendation_likes");

            migrationBuilder.DropIndex(
                name: "ix_recommendations_recommender_id_story_id",
                table: "recommendations");

            migrationBuilder.DropColumn(
                name: "like_count",
                table: "recommendations");

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_recommender_id",
                table: "recommendations",
                column: "recommender_id");
        }
    }
}
