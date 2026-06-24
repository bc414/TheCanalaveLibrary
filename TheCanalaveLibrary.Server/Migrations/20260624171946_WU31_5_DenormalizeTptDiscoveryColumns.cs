using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class WU31_5_DenormalizeTptDiscoveryColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Step 1: add columns to child tables ──────────────────────────────────────────────
            migrationBuilder.AddColumn<DateTime>(
                name: "date_created",
                table: "profile_blog_posts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<bool>(
                name: "is_published",
                table: "profile_blog_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_updated_date",
                table: "profile_blog_posts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<short>(
                name: "rating",
                table: "profile_blog_posts",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "date_created",
                table: "group_blog_posts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<bool>(
                name: "is_published",
                table: "group_blog_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_updated_date",
                table: "group_blog_posts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<short>(
                name: "rating",
                table: "group_blog_posts",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "date_posted",
                table: "chapter_comments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "date_posted",
                table: "blog_post_comments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "date_posted",
                table: "group_comments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTime>(
                name: "date_posted",
                table: "user_profile_comments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            // ── Step 2: copy existing data from base tables to child tables ───────────────────────
            migrationBuilder.Sql(@"
                UPDATE profile_blog_posts pbp
                SET date_created      = b.date_created,
                    last_updated_date = b.last_updated_date,
                    rating            = b.rating,
                    is_published      = b.is_published
                FROM base_blog_posts b
                WHERE b.blog_post_id = pbp.blog_post_id;

                UPDATE group_blog_posts gbp
                SET date_created      = b.date_created,
                    last_updated_date = b.last_updated_date,
                    rating            = b.rating,
                    is_published      = b.is_published
                FROM base_blog_posts b
                WHERE b.blog_post_id = gbp.blog_post_id;

                UPDATE chapter_comments cc
                SET date_posted = bc.date_posted
                FROM base_comments bc
                WHERE bc.comment_id = cc.comment_id;

                UPDATE blog_post_comments bpc
                SET date_posted = bc.date_posted
                FROM base_comments bc
                WHERE bc.comment_id = bpc.comment_id;

                UPDATE group_comments gc
                SET date_posted = bc.date_posted
                FROM base_comments bc
                WHERE bc.comment_id = gc.comment_id;

                UPDATE user_profile_comments upc
                SET date_posted = bc.date_posted
                FROM base_comments bc
                WHERE bc.comment_id = upc.comment_id;
            ");

            // ── Step 3: drop columns from base tables (data already copied above) ────────────────
            migrationBuilder.DropColumn(
                name: "date_created",
                table: "base_blog_posts");

            migrationBuilder.DropColumn(
                name: "is_published",
                table: "base_blog_posts");

            migrationBuilder.DropColumn(
                name: "last_updated_date",
                table: "base_blog_posts");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "base_blog_posts");

            migrationBuilder.DropColumn(
                name: "date_posted",
                table: "base_comments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ── Step 1: add columns back to base tables ───────────────────────────────────────────
            migrationBuilder.AddColumn<DateTime>(
                name: "date_created",
                table: "base_blog_posts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<bool>(
                name: "is_published",
                table: "base_blog_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_updated_date",
                table: "base_blog_posts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<short>(
                name: "rating",
                table: "base_blog_posts",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "date_posted",
                table: "base_comments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            // ── Step 2: copy data back from child tables to base tables ───────────────────────────
            migrationBuilder.Sql(@"
                UPDATE base_blog_posts b
                SET date_created      = COALESCE((SELECT pbp.date_created      FROM profile_blog_posts pbp WHERE pbp.blog_post_id = b.blog_post_id),
                                                 (SELECT gbp.date_created      FROM group_blog_posts   gbp WHERE gbp.blog_post_id = b.blog_post_id),
                                                 CURRENT_TIMESTAMP),
                    last_updated_date = COALESCE((SELECT pbp.last_updated_date FROM profile_blog_posts pbp WHERE pbp.blog_post_id = b.blog_post_id),
                                                 (SELECT gbp.last_updated_date FROM group_blog_posts   gbp WHERE gbp.blog_post_id = b.blog_post_id),
                                                 CURRENT_TIMESTAMP),
                    rating            = COALESCE((SELECT pbp.rating            FROM profile_blog_posts pbp WHERE pbp.blog_post_id = b.blog_post_id),
                                                 (SELECT gbp.rating            FROM group_blog_posts   gbp WHERE gbp.blog_post_id = b.blog_post_id),
                                                 0),
                    is_published      = COALESCE((SELECT pbp.is_published      FROM profile_blog_posts pbp WHERE pbp.blog_post_id = b.blog_post_id),
                                                 (SELECT gbp.is_published      FROM group_blog_posts   gbp WHERE gbp.blog_post_id = b.blog_post_id),
                                                 false);

                UPDATE base_comments bc
                SET date_posted = COALESCE(
                    (SELECT cc.date_posted  FROM chapter_comments      cc  WHERE cc.comment_id  = bc.comment_id),
                    (SELECT bpc.date_posted FROM blog_post_comments    bpc WHERE bpc.comment_id = bc.comment_id),
                    (SELECT gc.date_posted  FROM group_comments        gc  WHERE gc.comment_id  = bc.comment_id),
                    (SELECT upc.date_posted FROM user_profile_comments upc WHERE upc.comment_id = bc.comment_id),
                    CURRENT_TIMESTAMP);
            ");

            // ── Step 3: drop columns from child tables ────────────────────────────────────────────
            migrationBuilder.DropColumn(
                name: "date_created",
                table: "profile_blog_posts");

            migrationBuilder.DropColumn(
                name: "is_published",
                table: "profile_blog_posts");

            migrationBuilder.DropColumn(
                name: "last_updated_date",
                table: "profile_blog_posts");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "profile_blog_posts");

            migrationBuilder.DropColumn(
                name: "date_created",
                table: "group_blog_posts");

            migrationBuilder.DropColumn(
                name: "is_published",
                table: "group_blog_posts");

            migrationBuilder.DropColumn(
                name: "last_updated_date",
                table: "group_blog_posts");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "group_blog_posts");

            migrationBuilder.DropColumn(
                name: "date_posted",
                table: "chapter_comments");

            migrationBuilder.DropColumn(
                name: "date_posted",
                table: "blog_post_comments");

            migrationBuilder.DropColumn(
                name: "date_posted",
                table: "group_comments");

            migrationBuilder.DropColumn(
                name: "date_posted",
                table: "user_profile_comments");
        }
    }
}
