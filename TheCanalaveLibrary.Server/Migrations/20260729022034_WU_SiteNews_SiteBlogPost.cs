using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU_SiteNews_SiteBlogPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "site_blog_posts",
                columns: table => new
                {
                    blog_post_id = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    notify_all_users = table.Column<bool>(type: "boolean", nullable: false),
                    notified_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_blog_posts", x => x.blog_post_id);
                    table.ForeignKey(
                        name: "fk_site_blog_posts_base_blog_posts_blog_post_id",
                        column: x => x.blog_post_id,
                        principalTable: "base_blog_posts",
                        principalColumn: "blog_post_id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_blog_posts");
        }
    }
}
