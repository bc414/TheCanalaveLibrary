using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class L6_IndexBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_profile_comments_profile_user_id",
                table: "user_profile_comments");

            migrationBuilder.DropIndex(
                name: "ix_private_messages_conversation_id",
                table: "private_messages");

            migrationBuilder.DropIndex(
                name: "ix_notifications_recipient_user_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_group_comments_group_id",
                table: "group_comments");

            migrationBuilder.DropIndex(
                name: "ix_chapter_comments_chapter_id",
                table: "chapter_comments");

            migrationBuilder.DropIndex(
                name: "ix_blog_post_comments_blog_post_id",
                table: "blog_post_comments");

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interactions_completed",
                table: "user_story_interactions",
                column: "user_id",
                filter: "\"is_completed\" = true")
                .Annotation("Npgsql:IndexInclude", new[] { "story_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interactions_favorite",
                table: "user_story_interactions",
                column: "user_id",
                filter: "\"is_favorite\" = true")
                .Annotation("Npgsql:IndexInclude", new[] { "story_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interactions_followed",
                table: "user_story_interactions",
                column: "user_id",
                filter: "\"is_followed\" = true")
                .Annotation("Npgsql:IndexInclude", new[] { "story_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interactions_hidden_favorite",
                table: "user_story_interactions",
                column: "user_id",
                filter: "\"is_hidden_favorite\" = true")
                .Annotation("Npgsql:IndexInclude", new[] { "story_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interactions_ignored",
                table: "user_story_interactions",
                column: "user_id",
                filter: "\"is_ignored\" = true")
                .Annotation("Npgsql:IndexInclude", new[] { "story_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interactions_read_it_later",
                table: "user_story_interactions",
                column: "user_id",
                filter: "\"is_read_it_later\" = true")
                .Annotation("Npgsql:IndexInclude", new[] { "story_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_profile_comments_profile_user_id_date_posted",
                table: "user_profile_comments",
                columns: new[] { "profile_user_id", "date_posted" });

            migrationBuilder.CreateIndex(
                name: "ix_stories_last_updated_date",
                table: "stories",
                column: "last_updated_date");

            migrationBuilder.CreateIndex(
                name: "ix_stories_published_date",
                table: "stories",
                column: "published_date");

            migrationBuilder.CreateIndex(
                name: "ix_private_messages_conversation_id_date_sent",
                table: "private_messages",
                columns: new[] { "conversation_id", "date_sent" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_read_date",
                table: "notifications",
                columns: new[] { "recipient_user_id", "is_read", "date_created" });

            migrationBuilder.CreateIndex(
                name: "ix_group_comments_group_id_date_posted",
                table: "group_comments",
                columns: new[] { "group_id", "date_posted" });

            migrationBuilder.CreateIndex(
                name: "ix_chapter_comments_chapter_id_date_posted",
                table: "chapter_comments",
                columns: new[] { "chapter_id", "date_posted" });

            migrationBuilder.CreateIndex(
                name: "ix_blog_post_comments_blog_post_id_date_posted",
                table: "blog_post_comments",
                columns: new[] { "blog_post_id", "date_posted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_story_interactions_completed",
                table: "user_story_interactions");

            migrationBuilder.DropIndex(
                name: "ix_user_story_interactions_favorite",
                table: "user_story_interactions");

            migrationBuilder.DropIndex(
                name: "ix_user_story_interactions_followed",
                table: "user_story_interactions");

            migrationBuilder.DropIndex(
                name: "ix_user_story_interactions_hidden_favorite",
                table: "user_story_interactions");

            migrationBuilder.DropIndex(
                name: "ix_user_story_interactions_ignored",
                table: "user_story_interactions");

            migrationBuilder.DropIndex(
                name: "ix_user_story_interactions_read_it_later",
                table: "user_story_interactions");

            migrationBuilder.DropIndex(
                name: "ix_user_profile_comments_profile_user_id_date_posted",
                table: "user_profile_comments");

            migrationBuilder.DropIndex(
                name: "ix_stories_last_updated_date",
                table: "stories");

            migrationBuilder.DropIndex(
                name: "ix_stories_published_date",
                table: "stories");

            migrationBuilder.DropIndex(
                name: "ix_private_messages_conversation_id_date_sent",
                table: "private_messages");

            migrationBuilder.DropIndex(
                name: "ix_notifications_recipient_read_date",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_group_comments_group_id_date_posted",
                table: "group_comments");

            migrationBuilder.DropIndex(
                name: "ix_chapter_comments_chapter_id_date_posted",
                table: "chapter_comments");

            migrationBuilder.DropIndex(
                name: "ix_blog_post_comments_blog_post_id_date_posted",
                table: "blog_post_comments");

            migrationBuilder.CreateIndex(
                name: "ix_user_profile_comments_profile_user_id",
                table: "user_profile_comments",
                column: "profile_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_private_messages_conversation_id",
                table: "private_messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_user_id",
                table: "notifications",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_comments_group_id",
                table: "group_comments",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_chapter_comments_chapter_id",
                table: "chapter_comments",
                column: "chapter_id");

            migrationBuilder.CreateIndex(
                name: "ix_blog_post_comments_blog_post_id",
                table: "blog_post_comments",
                column: "blog_post_id");
        }
    }
}
