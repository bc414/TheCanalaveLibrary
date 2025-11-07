using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TheCanalaveLibrary.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "acknowledgment_roles",
                columns: table => new
                {
                    acknowledgment_role_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acknowledgment_roles", x => x.acknowledgment_role_id);
                });

            migrationBuilder.CreateTable(
                name: "also_favorited_scores",
                columns: table => new
                {
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    also_favorited_story_id = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_also_favorited_scores", x => new { x.story_id, x.also_favorited_story_id });
                });

            migrationBuilder.CreateTable(
                name: "also_recommended_scores",
                columns: table => new
                {
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    also_recommended_story_id = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_also_recommended_scores", x => new { x.story_id, x.also_recommended_story_id });
                });

            migrationBuilder.CreateTable(
                name: "badges",
                columns: table => new
                {
                    badge_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    icon_base_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_badges", x => x.badge_key);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    conversation_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    subject = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.conversation_id);
                });

            migrationBuilder.CreateTable(
                name: "notification_categories",
                columns: table => new
                {
                    notification_category_id = table.Column<short>(type: "smallint", nullable: false),
                    category_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_categories", x => x.notification_category_id);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_statuses",
                columns: table => new
                {
                    recommendation_status_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_statuses", x => x.recommendation_status_id);
                });

            migrationBuilder.CreateTable(
                name: "report_reasons",
                columns: table => new
                {
                    report_reason_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reason_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_reasons", x => x.report_reason_id);
                });

            migrationBuilder.CreateTable(
                name: "report_statuses",
                columns: table => new
                {
                    report_status_id = table.Column<short>(type: "smallint", nullable: false),
                    status_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_statuses", x => x.report_status_id);
                });

            migrationBuilder.CreateTable(
                name: "search_modes",
                columns: table => new
                {
                    search_mode_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_modes", x => x.search_mode_key);
                });

            migrationBuilder.CreateTable(
                name: "site_daily_stats",
                columns: table => new
                {
                    stat_date = table.Column<DateOnly>(type: "date", nullable: false),
                    new_users = table.Column<int>(type: "integer", nullable: false),
                    total_users = table.Column<int>(type: "integer", nullable: false),
                    new_stories = table.Column<int>(type: "integer", nullable: false),
                    total_stories = table.Column<int>(type: "integer", nullable: false),
                    new_words = table.Column<long>(type: "bigint", nullable: false),
                    total_words = table.Column<long>(type: "bigint", nullable: false),
                    new_recommendation_successes = table.Column<int>(type: "integer", nullable: false),
                    new_comments = table.Column<int>(type: "integer", nullable: false),
                    new_follows = table.Column<int>(type: "integer", nullable: false),
                    new_chapters = table.Column<int>(type: "integer", nullable: false),
                    new_recommendations_written = table.Column<int>(type: "integer", nullable: false),
                    page_views = table.Column<long>(type: "bigint", nullable: false),
                    active_users = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_site_daily_stats", x => x.stat_date);
                });

            migrationBuilder.CreateTable(
                name: "story_relationship_types",
                columns: table => new
                {
                    relationship_type_id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_relationship_types", x => x.relationship_type_id);
                });

            migrationBuilder.CreateTable(
                name: "story_statuses",
                columns: table => new
                {
                    story_status_id = table.Column<short>(type: "smallint", nullable: false),
                    status_name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_statuses", x => x.story_status_id);
                });

            migrationBuilder.CreateTable(
                name: "tag_types",
                columns: table => new
                {
                    tag_type_id = table.Column<short>(type: "smallint", nullable: false),
                    type_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tag_types", x => x.tag_type_id);
                });

            migrationBuilder.CreateTable(
                name: "themes",
                columns: table => new
                {
                    theme_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_themes", x => x.theme_id);
                });

            migrationBuilder.CreateTable(
                name: "user_interaction_filters",
                columns: table => new
                {
                    interaction_filter_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_interaction_filters", x => x.interaction_filter_key);
                });

            migrationBuilder.CreateTable(
                name: "user_story_tree_search_entries",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    is_authored_by_user = table.Column<bool>(type: "boolean", nullable: false),
                    is_public_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    is_hidden_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    is_recommendation = table.Column<bool>(type: "boolean", nullable: false),
                    is_hidden_gem = table.Column<bool>(type: "boolean", nullable: false),
                    is_author_spotlighted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_story_tree_search_entries", x => new { x.user_id, x.story_id });
                });

            migrationBuilder.CreateTable(
                name: "notification_types",
                columns: table => new
                {
                    notification_type_id = table.Column<short>(type: "smallint", nullable: false),
                    notification_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    default_email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    default_collapsed = table.Column<bool>(type: "boolean", nullable: false),
                    notification_category = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_types", x => x.notification_type_id);
                    table.ForeignKey(
                        name: "fk_notification_types_notification_categories_notification_cat",
                        column: x => x.notification_category,
                        principalTable: "notification_categories",
                        principalColumn: "notification_category_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    tag_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tag_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tag_type_id = table.Column<short>(type: "smallint", nullable: false),
                    is_fanon = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    parent_tag_id = table.Column<int>(type: "integer", nullable: true),
                    sprite_identifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    allow_oc_details = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.tag_id);
                    table.ForeignKey(
                        name: "fk_tags_tag_types_tag_type_id",
                        column: x => x.tag_type_id,
                        principalTable: "tag_types",
                        principalColumn: "tag_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tags_tags_parent_tag_id",
                        column: x => x.parent_tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_picture_relative_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    tagline = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    show_mature_content = table.Column<bool>(type: "boolean", nullable: false),
                    prefers_data_saver_mode = table.Column<bool>(type: "boolean", nullable: false),
                    prefers_animated_sprites = table.Column<bool>(type: "boolean", nullable: false),
                    allow_discovery_from_hidden_favorites = table.Column<bool>(type: "boolean", nullable: false),
                    theme_id = table.Column<int>(type: "integer", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false),
                    author_settings = table.Column<string>(type: "jsonb", nullable: false),
                    privacy_settings = table.Column<string>(type: "jsonb", nullable: false),
                    reader_settings = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_users_themes_theme_id",
                        column: x => x.theme_id,
                        principalTable: "themes",
                        principalColumn: "theme_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "default_search_settings",
                columns: table => new
                {
                    search_mode_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    interaction_filter_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_default_search_settings", x => new { x.search_mode_key, x.interaction_filter_key });
                    table.ForeignKey(
                        name: "fk_default_search_settings_search_modes_search_mode_key",
                        column: x => x.search_mode_key,
                        principalTable: "search_modes",
                        principalColumn: "search_mode_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_default_search_settings_user_interaction_filters_interactio",
                        column: x => x.interaction_filter_key,
                        principalTable: "user_interaction_filters",
                        principalColumn: "interaction_filter_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserPasskeys",
                columns: table => new
                {
                    credential_id = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_passkeys", x => x.credential_id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_passkeys_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "base_blog_posts",
                columns: table => new
                {
                    blog_post_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    author_id = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    view_count = table.Column<int>(type: "integer", nullable: false),
                    like_count = table.Column<int>(type: "integer", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    active_report_count = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_base_blog_posts", x => x.blog_post_id);
                    table.ForeignKey(
                        name: "fk_base_blog_posts_asp_net_users_author_id",
                        column: x => x.author_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "conversation_participants",
                columns: table => new
                {
                    conversation_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    last_read_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_participants", x => new { x.conversation_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_conversation_participants_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conversation_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "custom_lists",
                columns: table => new
                {
                    custom_list_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    list_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_lists", x => x.custom_list_id);
                    table.ForeignKey(
                        name: "fk_custom_lists_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "followed_users",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    followed_user_id = table.Column<int>(type: "integer", nullable: false),
                    date_followed = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    receive_alerts = table.Column<bool>(type: "boolean", nullable: false),
                    is_vouched = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_followed_users", x => new { x.user_id, x.followed_user_id });
                    table.ForeignKey(
                        name: "fk_followed_users_asp_net_users_followed_user_id",
                        column: x => x.followed_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_followed_users_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    group_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    creator_id = table.Column<int>(type: "integer", nullable: true),
                    group_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    max_content_rating = table.Column<short>(type: "smallint", nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_groups", x => x.group_id);
                    table.ForeignKey(
                        name: "fk_groups_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    notification_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    recipient_user_id = table.Column<int>(type: "integer", nullable: false),
                    notification_type_id = table.Column<short>(type: "smallint", nullable: false),
                    source_user_id = table.Column<int>(type: "integer", nullable: true),
                    related_entity_id = table.Column<int>(type: "integer", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.notification_id);
                    table.ForeignKey(
                        name: "fk_notifications_asp_net_users_recipient_user_id",
                        column: x => x.recipient_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_notifications_asp_net_users_source_user_id",
                        column: x => x.source_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notifications_notification_types_notification_type_id",
                        column: x => x.notification_type_id,
                        principalTable: "notification_types",
                        principalColumn: "notification_type_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "private_messages",
                columns: table => new
                {
                    message_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_id = table.Column<int>(type: "integer", nullable: false),
                    sender_user_id = table.Column<int>(type: "integer", nullable: true),
                    message_text = table.Column<string>(type: "text", nullable: false),
                    date_sent = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_private_messages", x => x.message_id);
                    table.ForeignKey(
                        name: "fk_private_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "conversation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_private_messages_users_sender_user_id",
                        column: x => x.sender_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    report_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reporter_user_id = table.Column<int>(type: "integer", nullable: true),
                    reported_entity_type = table.Column<short>(type: "smallint", nullable: false),
                    reported_entity_id = table.Column<int>(type: "integer", nullable: false),
                    report_reason_id = table.Column<short>(type: "smallint", nullable: false),
                    notes = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    report_status_id = table.Column<short>(type: "smallint", nullable: false),
                    moderator_user_id = table.Column<int>(type: "integer", nullable: true),
                    action_taken = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    date_reported = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    date_resolved = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.report_id);
                    table.ForeignKey(
                        name: "fk_reports_asp_net_users_moderator_user_id",
                        column: x => x.moderator_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_reports_asp_net_users_reporter_user_id",
                        column: x => x.reporter_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_reports_report_reasons_report_reason_id",
                        column: x => x.report_reason_id,
                        principalTable: "report_reasons",
                        principalColumn: "report_reason_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reports_report_statuses_report_status_id",
                        column: x => x.report_status_id,
                        principalTable: "report_statuses",
                        principalColumn: "report_status_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "saved_tag_selections",
                columns: table => new
                {
                    saved_tag_selection_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_tag_selections", x => x.saved_tag_selection_id);
                    table.ForeignKey(
                        name: "fk_saved_tag_selections_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "series",
                columns: table => new
                {
                    series_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    author_id = table.Column<int>(type: "integer", nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series", x => x.series_id);
                    table.ForeignKey(
                        name: "fk_series_users_author_id",
                        column: x => x.author_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "stories",
                columns: table => new
                {
                    story_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    author_id = table.Column<int>(type: "integer", nullable: true),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    story_status_id = table.Column<short>(type: "smallint", nullable: false),
                    word_count = table.Column<int>(type: "integer", nullable: false),
                    view_count = table.Column<int>(type: "integer", nullable: false),
                    published_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_published_date = table.Column<DateOnly>(type: "date", nullable: true),
                    original_last_updated_date = table.Column<DateOnly>(type: "date", nullable: true),
                    active_report_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stories", x => x.story_id);
                    table.ForeignKey(
                        name: "fk_stories_story_statuses_story_status_id",
                        column: x => x.story_status_id,
                        principalTable: "story_statuses",
                        principalColumn: "story_status_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stories_users_author_id",
                        column: x => x.author_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "user_badges",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    badge_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    date_earned = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    display_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_badges", x => new { x.user_id, x.badge_key });
                    table.ForeignKey(
                        name: "fk_user_badges_badges_badge_key",
                        column: x => x.badge_key,
                        principalTable: "badges",
                        principalColumn: "badge_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_badges_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_custom_filters",
                columns: table => new
                {
                    user_custom_filter_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    search_mode_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    filter_entity_type = table.Column<short>(type: "smallint", nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    include = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_custom_filters", x => x.user_custom_filter_id);
                    table.ForeignKey(
                        name: "fk_user_custom_filters_search_modes_search_mode_key",
                        column: x => x.search_mode_key,
                        principalTable: "search_modes",
                        principalColumn: "search_mode_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_custom_filters_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_notification_settings",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    notification_type_id = table.Column<short>(type: "smallint", nullable: false),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    collapsed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_notification_settings", x => new { x.user_id, x.notification_type_id });
                    table.ForeignKey(
                        name: "fk_user_notification_settings_notification_types_notification_",
                        column: x => x.notification_type_id,
                        principalTable: "notification_types",
                        principalColumn: "notification_type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_notification_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_profiles", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_search_settings",
                columns: table => new
                {
                    user_search_setting_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    search_mode_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    interaction_filter_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_search_settings", x => x.user_search_setting_id);
                    table.ForeignKey(
                        name: "fk_user_search_settings_search_modes_search_mode_key",
                        column: x => x.search_mode_key,
                        principalTable: "search_modes",
                        principalColumn: "search_mode_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_search_settings_user_interaction_filters_interaction_f",
                        column: x => x.interaction_filter_key,
                        principalTable: "user_interaction_filters",
                        principalColumn: "interaction_filter_key",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_search_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_stats",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    stories_read = table.Column<int>(type: "integer", nullable: false),
                    stories_in_progress = table.Column<int>(type: "integer", nullable: false),
                    stories_ignored = table.Column<int>(type: "integer", nullable: false),
                    chapters_read = table.Column<int>(type: "integer", nullable: false),
                    words_read = table.Column<int>(type: "integer", nullable: false),
                    recommendations_found_useful = table.Column<int>(type: "integer", nullable: false),
                    stories_written = table.Column<int>(type: "integer", nullable: false),
                    words_written = table.Column<long>(type: "bigint", nullable: false),
                    comments_written = table.Column<int>(type: "integer", nullable: false),
                    recommendations_written = table.Column<int>(type: "integer", nullable: false),
                    blog_posts_written = table.Column<int>(type: "integer", nullable: false),
                    acknowledged_as_beta_reader_count = table.Column<int>(type: "integer", nullable: false),
                    acknowledged_as_inspiration_count = table.Column<int>(type: "integer", nullable: false),
                    feature_contributions = table.Column<int>(type: "integer", nullable: false),
                    follower_count = table.Column<int>(type: "integer", nullable: false),
                    authors_followed = table.Column<int>(type: "integer", nullable: false),
                    favorites_on_stories = table.Column<int>(type: "integer", nullable: false),
                    views_on_stories = table.Column<long>(type: "bigint", nullable: false),
                    groups_joined = table.Column<int>(type: "integer", nullable: false),
                    recommendations_received = table.Column<int>(type: "integer", nullable: false),
                    spotlight_count = table.Column<int>(type: "integer", nullable: false),
                    active_report_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_stats", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_stats_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "base_blog_post_user",
                columns: table => new
                {
                    liked_blog_posts_blog_post_id = table.Column<int>(type: "integer", nullable: false),
                    liked_by_users_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_base_blog_post_user", x => new { x.liked_blog_posts_blog_post_id, x.liked_by_users_id });
                    table.ForeignKey(
                        name: "fk_base_blog_post_user_asp_net_users_liked_by_users_id",
                        column: x => x.liked_by_users_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_base_blog_post_user_blog_posts_liked_blog_posts_blog_post_id",
                        column: x => x.liked_blog_posts_blog_post_id,
                        principalTable: "base_blog_posts",
                        principalColumn: "blog_post_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "base_polls",
                columns: table => new
                {
                    poll_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    owner_id = table.Column<int>(type: "integer", nullable: false),
                    poll_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    date_opened = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    date_closed = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    base_blog_post_blog_post_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_base_polls", x => x.poll_id);
                    table.ForeignKey(
                        name: "fk_base_polls_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_base_polls_base_blog_posts_base_blog_post_blog_post_id",
                        column: x => x.base_blog_post_blog_post_id,
                        principalTable: "base_blog_posts",
                        principalColumn: "blog_post_id");
                });

            migrationBuilder.CreateTable(
                name: "group_blog_posts",
                columns: table => new
                {
                    blog_post_id = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_blog_posts", x => x.blog_post_id);
                    table.ForeignKey(
                        name: "fk_group_blog_posts_base_blog_posts_blog_post_id",
                        column: x => x.blog_post_id,
                        principalTable: "base_blog_posts",
                        principalColumn: "blog_post_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_blog_posts_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "group_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_folders",
                columns: table => new
                {
                    group_folder_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    max_rating = table.Column<short>(type: "smallint", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    parent_folder_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_folders", x => x.group_folder_id);
                    table.ForeignKey(
                        name: "fk_group_folders_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "group_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_members",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<short>(type: "smallint", nullable: false),
                    notify_for_new_story = table.Column<bool>(type: "boolean", nullable: false),
                    notify_for_new_blog_post = table.Column<bool>(type: "boolean", nullable: false),
                    date_joined = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_members", x => new { x.user_id, x.group_id });
                    table.ForeignKey(
                        name: "fk_group_members_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "group_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_tag_selection_entries",
                columns: table => new
                {
                    saved_tag_selection_entry_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    saved_tag_selection_id = table.Column<int>(type: "integer", nullable: false),
                    tag_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_tag_selection_entries", x => x.saved_tag_selection_entry_id);
                    table.ForeignKey(
                        name: "fk_saved_tag_selection_entries_saved_tag_selections_saved_tag_",
                        column: x => x.saved_tag_selection_id,
                        principalTable: "saved_tag_selections",
                        principalColumn: "saved_tag_selection_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_saved_tag_selection_entries_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "beta_readers",
                columns: table => new
                {
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    beta_reader_user_id = table.Column<int>(type: "integer", nullable: false),
                    date_added = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_beta_readers", x => new { x.story_id, x.beta_reader_user_id });
                    table.ForeignKey(
                        name: "fk_beta_readers_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_beta_readers_users_beta_reader_user_id",
                        column: x => x.beta_reader_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "co_authors",
                columns: table => new
                {
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    co_author_user_id = table.Column<int>(type: "integer", nullable: false),
                    date_added = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_co_authors", x => new { x.story_id, x.co_author_user_id });
                    table.ForeignKey(
                        name: "fk_co_authors_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_co_authors_users_co_author_user_id",
                        column: x => x.co_author_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_spotlights",
                columns: table => new
                {
                    spotlight_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    sponsoring_user_id = table.Column<int>(type: "integer", nullable: true),
                    sponsor_comment = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payment_id = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_community_spotlights", x => x.spotlight_id);
                    table.ForeignKey(
                        name: "fk_community_spotlights_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_community_spotlights_users_sponsoring_user_id",
                        column: x => x.sponsoring_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "custom_list_entries",
                columns: table => new
                {
                    list_id = table.Column<int>(type: "integer", nullable: false),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    date_added = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_list_entries", x => new { x.list_id, x.story_id });
                    table.ForeignKey(
                        name: "fk_custom_list_entries_custom_lists_list_id",
                        column: x => x.list_id,
                        principalTable: "custom_lists",
                        principalColumn: "custom_list_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_custom_list_entries_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_story_stats",
                columns: table => new
                {
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    stat_date = table.Column<DateOnly>(type: "date", nullable: false),
                    views = table.Column<int>(type: "integer", nullable: false),
                    favorites = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_story_stats", x => new { x.story_id, x.stat_date });
                    table.ForeignKey(
                        name: "fk_daily_story_stats_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_stories",
                columns: table => new
                {
                    group_story_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<int>(type: "integer", nullable: false),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    added_by_user_id = table.Column<int>(type: "integer", nullable: true),
                    date_added = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_stories", x => x.group_story_id);
                    table.ForeignKey(
                        name: "fk_group_stories_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "group_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_stories_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_stories_users_added_by_user_id",
                        column: x => x.added_by_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "profile_blog_posts",
                columns: table => new
                {
                    blog_post_id = table.Column<int>(type: "integer", nullable: false),
                    story_id = table.Column<int>(type: "integer", nullable: true),
                    has_spoilers = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_profile_blog_posts", x => x.blog_post_id);
                    table.ForeignKey(
                        name: "fk_profile_blog_posts_base_blog_posts_blog_post_id",
                        column: x => x.blog_post_id,
                        principalTable: "base_blog_posts",
                        principalColumn: "blog_post_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_profile_blog_posts_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "recommendations",
                columns: table => new
                {
                    recommendation_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    recommender_id = table.Column<int>(type: "integer", nullable: true),
                    status_id = table.Column<short>(type: "smallint", nullable: false),
                    is_hidden_gem = table.Column<bool>(type: "boolean", nullable: false),
                    is_highlighted_by_author = table.Column<bool>(type: "boolean", nullable: false),
                    successful_rec_count = table.Column<int>(type: "integer", nullable: false),
                    date_posted = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    active_report_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendations", x => x.recommendation_id);
                    table.ForeignKey(
                        name: "fk_recommendations_recommendation_statuses_status_id",
                        column: x => x.status_id,
                        principalTable: "recommendation_statuses",
                        principalColumn: "recommendation_status_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recommendations_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recommendations_users_recommender_id",
                        column: x => x.recommender_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "series_entries",
                columns: table => new
                {
                    series_id = table.Column<int>(type: "integer", nullable: false),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_series_entries", x => new { x.series_id, x.story_id });
                    table.ForeignKey(
                        name: "fk_series_entries_series_series_id",
                        column: x => x.series_id,
                        principalTable: "series",
                        principalColumn: "series_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_series_entries_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "setting_details",
                columns: table => new
                {
                    setting_detail_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    base_tag_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    description = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_setting_details", x => x.setting_detail_id);
                    table.ForeignKey(
                        name: "fk_setting_details_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_setting_details_tags_base_tag_id",
                        column: x => x.base_tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "story_acknowledgments",
                columns: table => new
                {
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    acknowledged_user_id = table.Column<int>(type: "integer", nullable: false),
                    acknowledgment_role_id = table.Column<short>(type: "smallint", nullable: false),
                    date_acknowledged = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_acknowledgments", x => new { x.story_id, x.acknowledged_user_id, x.acknowledgment_role_id });
                    table.ForeignKey(
                        name: "fk_story_acknowledgments_acknowledgment_roles_acknowledgment_r",
                        column: x => x.acknowledgment_role_id,
                        principalTable: "acknowledgment_roles",
                        principalColumn: "acknowledgment_role_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_story_acknowledgments_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_story_acknowledgments_users_acknowledged_user_id",
                        column: x => x.acknowledged_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_arcs",
                columns: table => new
                {
                    story_arc_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    start_chapter_number = table.Column<int>(type: "integer", nullable: false),
                    end_chapter_number = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_arcs", x => x.story_arc_id);
                    table.ForeignKey(
                        name: "fk_story_arcs_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_character_relationships",
                columns: table => new
                {
                    story_character_relationship_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    relationship_type = table.Column<short>(type: "smallint", nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_character_relationships", x => x.story_character_relationship_id);
                    table.ForeignKey(
                        name: "fk_story_character_relationships_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_characters",
                columns: table => new
                {
                    story_character_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    character_tag_id = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false),
                    is_oc = table.Column<bool>(type: "boolean", nullable: false),
                    oc_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    oc_bio = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_characters", x => x.story_character_id);
                    table.ForeignKey(
                        name: "fk_story_characters_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_story_characters_tags_character_tag_id",
                        column: x => x.character_tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "story_details",
                columns: table => new
                {
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    long_description = table.Column<string>(type: "text", nullable: true),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    post_approval_status = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_details", x => x.story_id);
                    table.ForeignKey(
                        name: "fk_story_details_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_imports",
                columns: table => new
                {
                    import_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    source_platform = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    verification_status = table.Column<short>(type: "smallint", nullable: false),
                    date_imported = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_imports", x => x.import_id);
                    table.ForeignKey(
                        name: "fk_story_imports_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_listings",
                columns: table => new
                {
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    story_title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    short_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cover_art_relative_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: false, computedColumnSql: "to_tsvector('english', coalesce(\"story_title\", '') || ' ' || coalesce(\"short_description\", ''))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_listings", x => x.story_id);
                    table.ForeignKey(
                        name: "fk_story_listings_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_relationships",
                columns: table => new
                {
                    source_story_id = table.Column<int>(type: "integer", nullable: false),
                    target_story_id = table.Column<int>(type: "integer", nullable: false),
                    relationship_type_id = table.Column<short>(type: "smallint", nullable: false),
                    status_id = table.Column<short>(type: "smallint", nullable: false),
                    date_created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_relationships", x => new { x.source_story_id, x.target_story_id, x.relationship_type_id });
                    table.ForeignKey(
                        name: "fk_story_relationships_stories_source_story_id",
                        column: x => x.source_story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_story_relationships_stories_target_story_id",
                        column: x => x.target_story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_story_relationships_story_relationship_types_relationship_t",
                        column: x => x.relationship_type_id,
                        principalTable: "story_relationship_types",
                        principalColumn: "relationship_type_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "story_tags",
                columns: table => new
                {
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    tag_id = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_tags", x => new { x.story_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_story_tags_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_story_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "tag_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "blog_post_polls",
                columns: table => new
                {
                    poll_id = table.Column<int>(type: "integer", nullable: false),
                    blog_post_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blog_post_polls", x => x.poll_id);
                    table.ForeignKey(
                        name: "fk_blog_post_polls_base_blog_posts_blog_post_id",
                        column: x => x.blog_post_id,
                        principalTable: "base_blog_posts",
                        principalColumn: "blog_post_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_blog_post_polls_base_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "base_polls",
                        principalColumn: "poll_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_options",
                columns: table => new
                {
                    poll_option_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    text = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    poll_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_poll_options", x => x.poll_option_id);
                    table.ForeignKey(
                        name: "fk_poll_options_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "base_polls",
                        principalColumn: "poll_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "site_polls",
                columns: table => new
                {
                    poll_id = table.Column<int>(type: "integer", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_polls", x => x.poll_id);
                    table.ForeignKey(
                        name: "fk_site_polls_base_polls_poll_id",
                        column: x => x.poll_id,
                        principalTable: "base_polls",
                        principalColumn: "poll_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_folder_group_story",
                columns: table => new
                {
                    group_folders_group_folder_id = table.Column<int>(type: "integer", nullable: false),
                    group_stories_group_story_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_group_folder_group_story", x => new { x.group_folders_group_folder_id, x.group_stories_group_story_id });
                    table.ForeignKey(
                        name: "fk_group_folder_group_story_group_folders_group_folders_group_",
                        column: x => x.group_folders_group_folder_id,
                        principalTable: "group_folders",
                        principalColumn: "group_folder_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_folder_group_story_group_stories_group_stories_group_",
                        column: x => x.group_stories_group_story_id,
                        principalTable: "group_stories",
                        principalColumn: "group_story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_details",
                columns: table => new
                {
                    recommendation_id = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_details", x => x.recommendation_id);
                    table.ForeignKey(
                        name: "fk_recommendation_details_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "recommendation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_successes",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    recommendation_id = table.Column<int>(type: "integer", nullable: false),
                    date_recorded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_successes", x => new { x.user_id, x.recommendation_id });
                    table.ForeignKey(
                        name: "fk_recommendation_successes_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "recommendation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recommendation_successes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_story_interactions",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    is_in_progress = table.Column<bool>(type: "boolean", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    is_actively_reading = table.Column<bool>(type: "boolean", nullable: false),
                    is_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    is_hidden_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    is_followed = table.Column<bool>(type: "boolean", nullable: false),
                    is_read_it_later = table.Column<bool>(type: "boolean", nullable: false),
                    is_ignored = table.Column<bool>(type: "boolean", nullable: false),
                    recommendation_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_story_interactions", x => new { x.user_id, x.story_id });
                    table.ForeignKey(
                        name: "fk_user_story_interactions_recommendations_recommendation_id",
                        column: x => x.recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "recommendation_id");
                    table.ForeignKey(
                        name: "fk_user_story_interactions_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_story_interactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "story_character_story_character_relationship",
                columns: table => new
                {
                    story_character_relationships_story_character_relationship_id = table.Column<int>(type: "integer", nullable: false),
                    story_characters_story_character_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_story_character_story_character_relationship", x => new { x.story_character_relationships_story_character_relationship_id, x.story_characters_story_character_id });
                    table.ForeignKey(
                        name: "fk_story_character_story_character_relationship_story_characte",
                        column: x => x.story_character_relationships_story_character_relationship_id,
                        principalTable: "story_character_relationships",
                        principalColumn: "story_character_relationship_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_story_character_story_character_relationship_story_characte1",
                        column: x => x.story_characters_story_character_id,
                        principalTable: "story_characters",
                        principalColumn: "story_character_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_option_user",
                columns: table => new
                {
                    poll_option_id = table.Column<int>(type: "integer", nullable: false),
                    voters_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_poll_option_user", x => new { x.poll_option_id, x.voters_id });
                    table.ForeignKey(
                        name: "fk_poll_option_user_asp_net_users_voters_id",
                        column: x => x.voters_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_poll_option_user_poll_options_poll_option_id",
                        column: x => x.poll_option_id,
                        principalTable: "poll_options",
                        principalColumn: "poll_option_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_story_interaction_dates",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    favorite_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    hidden_favorite_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    followed_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    read_it_later_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ignored_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_story_interaction_dates", x => new { x.user_id, x.story_id });
                    table.ForeignKey(
                        name: "fk_user_story_interaction_dates_user_story_interactions_user_i",
                        columns: x => new { x.user_id, x.story_id },
                        principalTable: "user_story_interactions",
                        principalColumns: new[] { "user_id", "story_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_story_recommendation_sources",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    source_recommendation_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_story_recommendation_sources", x => new { x.user_id, x.story_id });
                    table.ForeignKey(
                        name: "fk_user_story_recommendation_sources_recommendations_source_re",
                        column: x => x.source_recommendation_id,
                        principalTable: "recommendations",
                        principalColumn: "recommendation_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_story_recommendation_sources_user_story_interactions_u",
                        columns: x => new { x.user_id, x.story_id },
                        principalTable: "user_story_interactions",
                        principalColumns: new[] { "user_id", "story_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "base_comment_user",
                columns: table => new
                {
                    liked_by_users_id = table.Column<int>(type: "integer", nullable: false),
                    liked_comments_comment_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_base_comment_user", x => new { x.liked_by_users_id, x.liked_comments_comment_id });
                    table.ForeignKey(
                        name: "fk_base_comment_user_asp_net_users_liked_by_users_id",
                        column: x => x.liked_by_users_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "base_comments",
                columns: table => new
                {
                    comment_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    parent_comment_id = table.Column<long>(type: "bigint", nullable: true),
                    comment_text = table.Column<string>(type: "text", nullable: false),
                    date_posted = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    like_count = table.Column<int>(type: "integer", nullable: false),
                    active_report_count = table.Column<int>(type: "integer", nullable: false),
                    blog_post_comment_comment_id = table.Column<long>(type: "bigint", nullable: true),
                    chapter_comment_comment_id = table.Column<long>(type: "bigint", nullable: true),
                    group_comment_comment_id = table.Column<long>(type: "bigint", nullable: true),
                    user_profile_comment_comment_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_base_comments", x => x.comment_id);
                    table.ForeignKey(
                        name: "fk_base_comments_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_base_comments_base_comments_parent_comment_id",
                        column: x => x.parent_comment_id,
                        principalTable: "base_comments",
                        principalColumn: "comment_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "blog_post_comments",
                columns: table => new
                {
                    comment_id = table.Column<long>(type: "bigint", nullable: false),
                    blog_post_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blog_post_comments", x => x.comment_id);
                    table.ForeignKey(
                        name: "fk_blog_post_comments_base_blog_posts_blog_post_id",
                        column: x => x.blog_post_id,
                        principalTable: "base_blog_posts",
                        principalColumn: "blog_post_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_blog_post_comments_base_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "base_comments",
                        principalColumn: "comment_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "feature_contributions",
                columns: table => new
                {
                    feature_contribution_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    comment_id = table.Column<long>(type: "bigint", nullable: true),
                    blog_post_id = table.Column<int>(type: "integer", nullable: true),
                    feature_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    date_awarded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_feature_contributions", x => x.feature_contribution_id);
                    table.ForeignKey(
                        name: "fk_feature_contributions_base_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "base_comments",
                        principalColumn: "comment_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_feature_contributions_blog_posts_blog_post_id",
                        column: x => x.blog_post_id,
                        principalTable: "base_blog_posts",
                        principalColumn: "blog_post_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_feature_contributions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "group_comments",
                columns: table => new
                {
                    comment_id = table.Column<long>(type: "bigint", nullable: false),
                    group_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_comments", x => x.comment_id);
                    table.ForeignKey(
                        name: "fk_group_comments_base_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "base_comments",
                        principalColumn: "comment_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_group_comments_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "groups",
                        principalColumn: "group_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_profile_comments",
                columns: table => new
                {
                    comment_id = table.Column<long>(type: "bigint", nullable: false),
                    profile_user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profile_comments", x => x.comment_id);
                    table.ForeignKey(
                        name: "fk_user_profile_comments_asp_net_users_profile_user_id",
                        column: x => x.profile_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_profile_comments_base_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "base_comments",
                        principalColumn: "comment_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chapter_comments",
                columns: table => new
                {
                    comment_id = table.Column<long>(type: "bigint", nullable: false),
                    chapter_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter_comments", x => x.comment_id);
                    table.ForeignKey(
                        name: "fk_chapter_comments_base_comments_comment_id",
                        column: x => x.comment_id,
                        principalTable: "base_comments",
                        principalColumn: "comment_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "chapter_contents",
                columns: table => new
                {
                    chapter_content_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chapter_id = table.Column<int>(type: "integer", nullable: false),
                    author_id = table.Column<int>(type: "integer", nullable: true),
                    version_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    top_authors_note = table.Column<string>(type: "text", nullable: true),
                    chapter_text = table.Column<string>(type: "text", nullable: false),
                    bottom_authors_note = table.Column<string>(type: "text", nullable: true),
                    word_count = table.Column<int>(type: "integer", nullable: false),
                    view_count = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    publish_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_publish_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chapter_contents", x => x.chapter_content_id);
                    table.ForeignKey(
                        name: "fk_chapter_contents_users_author_id",
                        column: x => x.author_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "chapters",
                columns: table => new
                {
                    chapter_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    story_id = table.Column<int>(type: "integer", nullable: false),
                    chapter_number = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    primary_content_id = table.Column<long>(type: "bigint", nullable: false),
                    is_published = table.Column<bool>(type: "boolean", nullable: false),
                    version_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chapters", x => x.chapter_id);
                    table.ForeignKey(
                        name: "fk_chapters_chapter_contents_primary_content_id",
                        column: x => x.primary_content_id,
                        principalTable: "chapter_contents",
                        principalColumn: "chapter_content_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_chapters_stories_story_id",
                        column: x => x.story_id,
                        principalTable: "stories",
                        principalColumn: "story_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_chapter_interactions",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    chapter_id = table.Column<int>(type: "integer", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_progress = table.Column<float>(type: "real", nullable: false),
                    last_interaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_chapter_interactions", x => new { x.user_id, x.chapter_id });
                    table.ForeignKey(
                        name: "fk_user_chapter_interactions_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalTable: "chapters",
                        principalColumn: "chapter_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_chapter_interactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "id", "concurrency_stamp", "name", "normalized_name", "user_id" },
                values: new object[,]
                {
                    { 1, "1", "User", "USER", null },
                    { 2, "2", "Moderator", "MODERATOR", null },
                    { 3, "3", "Admin", "ADMIN", null }
                });

            migrationBuilder.InsertData(
                table: "acknowledgment_roles",
                columns: new[] { "acknowledgment_role_id", "role_name" },
                values: new object[,]
                {
                    { (short)1, "Beta Reader" },
                    { (short)2, "Planner" },
                    { (short)3, "Cover Artist" },
                    { (short)4, "Editor" },
                    { (short)5, "Inspiration" }
                });

            migrationBuilder.InsertData(
                table: "badges",
                columns: new[] { "badge_key", "description", "display_name", "icon_base_url", "sort_order" },
                values: new object[,]
                {
                    { "Architect", "Helped develop a site feature", "Architect", "icons/badges/architect.png", 4 },
                    { "Artist", "Made cover art for others", "Artist", "icons/badges/artist.png", 5 },
                    { "BetaReader", "Acknowledged as a Beta Reader on stories.", "Beta Reader", "icons/badges/beta.png", 1 },
                    { "Patron", "Supported the site through Community Spotlight.", "Patron", "icons/badges/patron.png", 2 },
                    { "Recommender", "Has many successful recs", "Recommender", "icons/badges/recommender.png", 3 }
                });

            migrationBuilder.InsertData(
                table: "notification_categories",
                columns: new[] { "notification_category_id", "category_name", "description", "sort_order" },
                values: new object[,]
                {
                    { (short)0, "Site News", "Announcements and updates from the site staff.", 1 },
                    { (short)1, "Followed Content", "Updates from authors, stories, and recommendations you follow.", 2 },
                    { (short)2, "Your Stories", "Interactions with stories you have written.", 3 },
                    { (short)3, "Your Profile", "Interactions with your user profile.", 4 },
                    { (short)4, "Your Recommendations", "Updates on recommendations you have written.", 5 },
                    { (short)5, "Collaborations", "Updates related to co-authoring and beta reading.", 6 },
                    { (short)6, "Groups", "Notifications from groups you are a member of.", 7 },
                    { (short)7, "Warnings", "Alerts related to your account or content.", 8 },
                    { (short)8, "Your Reports", "Updates on reports you have submitted.", 9 }
                });

            migrationBuilder.InsertData(
                table: "recommendation_statuses",
                columns: new[] { "recommendation_status_id", "description", "status_name" },
                values: new object[,]
                {
                    { (short)1, "Submitted by user, awaiting author review.", "Pending Approval" },
                    { (short)2, "Publicly visible.", "Approved" },
                    { (short)3, "Rejected by author, not visible.", "Rejected" },
                    { (short)4, "An approved recommendation that was reported and is under review.", "Under Review" }
                });

            migrationBuilder.InsertData(
                table: "report_reasons",
                columns: new[] { "report_reason_id", "description", "reason_name" },
                values: new object[,]
                {
                    { (short)1, "A reason not covered by other categories.", "Other" },
                    { (short)2, "Unsolicited advertising or repeated, low-effort content.", "Spam" },
                    { (short)3, "Content that attacks a person or group based on race, ethnicity, religion, etc.", "Hate Speech" },
                    { (short)4, "Targeted abuse, bullying, or intimidation of a user.", "Harassment" },
                    { (short)5, "Content violating laws, such as child pornography or piracy.", "Illegal Content" },
                    { (short)6, "Posting content that is not your own without attribution.", "Plagiarism" }
                });

            migrationBuilder.InsertData(
                table: "report_statuses",
                columns: new[] { "report_status_id", "status_name" },
                values: new object[,]
                {
                    { (short)0, "Open" },
                    { (short)1, "Under Review" },
                    { (short)2, "Resolved - No Action" },
                    { (short)3, "Resolved - Action Taken" }
                });

            migrationBuilder.InsertData(
                table: "search_modes",
                columns: new[] { "search_mode_key", "description", "name" },
                values: new object[,]
                {
                    { "AlsoFavorited", "Find stories favorited by users who also favorited your selection.", "Also Favorited" },
                    { "DefaultSearch", "The regular search mode with tags and search result orders.", "Default Search" },
                    { "RandomSearch", "Find a random story based on your filters.", "Random Search" },
                    { "TreeSearch", "Discover stories through connections: favorites, recommendations, and author follows.", "Tree Search" }
                });

            migrationBuilder.InsertData(
                table: "story_relationship_types",
                columns: new[] { "relationship_type_id", "type_name" },
                values: new object[,]
                {
                    { (short)1, "Inspired By" },
                    { (short)2, "Prequel" },
                    { (short)3, "Sequel" },
                    { (short)4, "Companion Piece" }
                });

            migrationBuilder.InsertData(
                table: "story_statuses",
                columns: new[] { "story_status_id", "description", "status_name" },
                values: new object[,]
                {
                    { (short)0, "Story is a work in progress and not visible to the public.", "Draft" },
                    { (short)1, "Story has been submitted and is awaiting moderator approval.", "Pending Approval" },
                    { (short)2, "Story is approved, public, and actively being updated.", "In Progress" },
                    { (short)3, "The story is finished.", "Completed" },
                    { (short)4, "The author is taking a break from updating.", "On Hiatus" },
                    { (short)5, "The story will not be continued.", "Cancelled" },
                    { (short)6, "The story is undergoing major revisions.", "Rewriting" },
                    { (short)7, "Story is visible to beta readers for feedback.", "Open Beta" },
                    { (short)8, "Story was submitted but did not pass moderation.", "Rejected" }
                });

            migrationBuilder.InsertData(
                table: "tag_types",
                columns: new[] { "tag_type_id", "type_name" },
                values: new object[,]
                {
                    { (short)0, "Character" },
                    { (short)1, "Setting" },
                    { (short)2, "Genre" },
                    { (short)3, "Content Warning" },
                    { (short)4, "Crossover Fandom" },
                    { (short)5, "Relationship" }
                });

            migrationBuilder.InsertData(
                table: "themes",
                columns: new[] { "theme_id", "description", "name" },
                values: new object[] { 1, "The default Pokémon theme!", "Pokémon" });

            migrationBuilder.InsertData(
                table: "user_interaction_filters",
                columns: new[] { "interaction_filter_key", "description", "name" },
                values: new object[,]
                {
                    { "Completed", "Exclude stories you have already finished.", "Completed" },
                    { "Favorited", "Exclude stories on your 'Favorite' list.", "Favorited" },
                    { "Followed", "Exclude stories you are 'Following'.", "Followed" },
                    { "HiddenFavorited", "Exclude stories on your 'Hidden Favorite' list.", "Hidden Favorite" },
                    { "Ignored", "Exclude stories you have marked as 'Ignored'.", "Ignored" },
                    { "InProgress", "Exclude stories on your 'In Progress' list.", "In Progress" },
                    { "ReadItLater", "Exclude stories on your 'Read It Later' list.", "Read It Later" }
                });

            migrationBuilder.InsertData(
                table: "default_search_settings",
                columns: new[] { "interaction_filter_key", "search_mode_key", "is_enabled" },
                values: new object[,]
                {
                    { "Completed", "RandomSearch", false },
                    { "Ignored", "RandomSearch", true },
                    { "Completed", "TreeSearch", true },
                    { "Ignored", "TreeSearch", true },
                    { "ReadItLater", "TreeSearch", false }
                });

            migrationBuilder.InsertData(
                table: "notification_types",
                columns: new[] { "notification_type_id", "default_collapsed", "default_email_enabled", "description", "display_name", "notification_category", "notification_key" },
                values: new object[,]
                {
                    { (short)0, false, false, "A new announcement from site staff.", "Site Announcement", (short)0, "SiteAnnouncement" },
                    { (short)10, false, true, "A story you follow has a new chapter.", "New Chapter", (short)1, "NewChapterOnFollowedStory" },
                    { (short)11, false, true, "An author you follow posted a new story.", "New Story", (short)1, "NewStoryByFollowedUser" },
                    { (short)12, false, false, "An author you follow posted a new recommendation.", "New Recommendation by Followed User", (short)1, "NewRecommendationByFollowedUser" },
                    { (short)13, false, false, "An author you follow posted a new blog post.", "New Blog Post", (short)1, "NewBlogPostByFollowedUser" },
                    { (short)14, false, false, "A story you follow has a new blog post.", "New Story Blog Post", (short)1, "NewBlogPostOnFollowedStory" },
                    { (short)15, false, false, "A story you favorited has a new blog post.", "Blog Post on Favorited Story", (short)1, "NewBlogPostOnFavoritedStory" },
                    { (short)16, false, false, "A story on your 'Read Later' list has a new blog post.", "Blog Post on 'Read Later' Story", (short)1, "NewBlogPostOnReadItLaterStory" },
                    { (short)20, false, true, "Someone favorited one of your stories.", "New Favorite", (short)2, "NewStoryFavorite" },
                    { (short)21, false, true, "Someone followed one of your stories.", "New Story Follower", (short)2, "NewStoryFollower" },
                    { (short)22, false, true, "Someone recommended one of your stories.", "New Recommendation on Your Story", (short)2, "NewRecommendationOnYourStory" },
                    { (short)23, false, true, "A recommendation on your story was designated as a 'Hidden Gem'.", "Hidden Gem", (short)2, "HiddenGem" },
                    { (short)24, false, true, "You received a new comment on one of your story chapters.", "New Story Comment", (short)2, "NewStoryComment" },
                    { (short)25, false, false, "Your story was added to a group's collection.", "Story Added to Group", (short)2, "YourStoryAddedToGroup" },
                    { (short)26, false, false, "One of your OC tags matches a new fanon tag.", "Tag Update Suggestion", (short)2, "TagUpdateSuggestion" },
                    { (short)30, false, true, "A new user is following you.", "New Profile Follower", (short)3, "NewFollowerOnYou" },
                    { (short)31, false, true, "You received a new comment on your profile.", "New Profile Comment", (short)3, "NewCommentOnYourProfile" },
                    { (short)32, false, false, "A user you follow vouched for you.", "New Vouch", (short)3, "NewVouchOnYou" },
                    { (short)33, false, true, "You received a new comment on your blog post.", "New Blog Comment", (short)3, "NewCommentOnBlog" },
                    { (short)34, false, true, "Someone replied to your comment.", "New Reply", (short)3, "CommentReply" },
                    { (short)40, false, true, "An author approved your recommendation.", "Recommendation Approved", (short)4, "RecommendationApproved" },
                    { (short)41, false, true, "An author highlighted your recommendation.", "Recommendation Highlighted", (short)4, "RecommendationHighlighted" },
                    { (short)42, false, true, "A user marked your recommendation as helpful.", "Successful Recommendation", (short)4, "SuccessfulRec" },
                    { (short)50, false, true, "An author wants to link their story to yours.", "New Story Relationship Request", (short)5, "StoryRelationshipRequested" },
                    { (short)51, false, true, "Your request to link to another story was approved.", "Story Relationship Approved", (short)5, "StoryRelationshipApproved" },
                    { (short)52, false, true, "You were acknowledged as a contributor on a new story.", "New Acknowledgment", (short)5, "NewStoryAcknowledgement" },
                    { (short)60, false, false, "A new story was added to a group you're in.", "New Group Story", (short)6, "NewGroupStory" },
                    { (short)61, false, false, "A new blog post was made in a group you're in.", "New Group Blog Post", (short)6, "NewGroupBlogPost" },
                    { (short)70, false, true, "Your content was removed for a ToS violation.", "Content Removed", (short)7, "ContentRemoved" },
                    { (short)71, false, true, "Your story submission was rejected.", "Story Rejected", (short)7, "StoryRejected" },
                    { (short)72, false, true, "You have received an official warning.", "Account Warning", (short)7, "AccountWarning" },
                    { (short)73, false, true, "Your account has been temporarily suspended.", "Account Suspended", (short)7, "AccountSuspended" },
                    { (short)74, false, true, "Your account has been permanently banned.", "Account Banned", (short)7, "AccountBanned" },
                    { (short)80, false, false, "Thank you, we have received your report.", "Report Received", (short)8, "ReportReceived" },
                    { (short)81, false, false, "Your report has been resolved and action was taken.", "Report Resolved (Action Taken)", (short)8, "ReportResolved" },
                    { (short)82, false, false, "Your report was reviewed, but no action was deemed necessary.", "Report Resolved (No Action)", (short)8, "ReportResolvedNoAction" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "AspNetRoleClaims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_roles_user_id",
                table: "AspNetRoles",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "AspNetUserClaims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "AspNetUserLogins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_passkeys_user_id",
                table: "AspNetUserPasskeys",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "AspNetUserRoles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_theme_id",
                table: "AspNetUsers",
                column: "theme_id");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_badges_display_name",
                table: "badges",
                column: "display_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_base_blog_post_user_liked_by_users_id",
                table: "base_blog_post_user",
                column: "liked_by_users_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_blog_posts_author_id",
                table: "base_blog_posts",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_comment_user_liked_comments_comment_id",
                table: "base_comment_user",
                column: "liked_comments_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_comments_blog_post_comment_comment_id",
                table: "base_comments",
                column: "blog_post_comment_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_comments_chapter_comment_comment_id",
                table: "base_comments",
                column: "chapter_comment_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_comments_group_comment_comment_id",
                table: "base_comments",
                column: "group_comment_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_comments_parent_comment_id",
                table: "base_comments",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_comments_user_id",
                table: "base_comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_comments_user_profile_comment_comment_id",
                table: "base_comments",
                column: "user_profile_comment_comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_polls_base_blog_post_blog_post_id",
                table: "base_polls",
                column: "base_blog_post_blog_post_id");

            migrationBuilder.CreateIndex(
                name: "ix_base_polls_owner_id",
                table: "base_polls",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_beta_readers_beta_reader_user_id",
                table: "beta_readers",
                column: "beta_reader_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_blog_post_comments_blog_post_id",
                table: "blog_post_comments",
                column: "blog_post_id");

            migrationBuilder.CreateIndex(
                name: "ix_blog_post_polls_blog_post_id",
                table: "blog_post_polls",
                column: "blog_post_id");

            migrationBuilder.CreateIndex(
                name: "ix_chapter_comments_chapter_id",
                table: "chapter_comments",
                column: "chapter_id");

            migrationBuilder.CreateIndex(
                name: "ix_chapter_contents_author_id",
                table: "chapter_contents",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_chapter_contents_chapter_id_sort_order",
                table: "chapter_contents",
                columns: new[] { "chapter_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chapters_primary_content_id",
                table: "chapters",
                column: "primary_content_id");

            migrationBuilder.CreateIndex(
                name: "ix_chapters_story_id_chapter_number",
                table: "chapters",
                columns: new[] { "story_id", "chapter_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_co_authors_co_author_user_id",
                table: "co_authors",
                column: "co_author_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_spotlights_sponsoring_user_id",
                table: "community_spotlights",
                column: "sponsoring_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_spotlights_story_id",
                table: "community_spotlights",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_participants_user_id",
                table: "conversation_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_list_entries_story_id",
                table: "custom_list_entries",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_lists_user_id_list_name",
                table: "custom_lists",
                columns: new[] { "user_id", "list_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_default_search_settings_interaction_filter_key",
                table: "default_search_settings",
                column: "interaction_filter_key");

            migrationBuilder.CreateIndex(
                name: "ix_feature_contributions_blog_post_id",
                table: "feature_contributions",
                column: "blog_post_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_contributions_comment_id",
                table: "feature_contributions",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_feature_contributions_user_id",
                table: "feature_contributions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_followed_users_followed_user_id",
                table: "followed_users",
                column: "followed_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_blog_posts_group_id",
                table: "group_blog_posts",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_comments_group_id",
                table: "group_comments",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_folder_group_story_group_stories_group_story_id",
                table: "group_folder_group_story",
                column: "group_stories_group_story_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_folders_group_id_parent_folder_id_name",
                table: "group_folders",
                columns: new[] { "group_id", "parent_folder_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_group_members_group_id",
                table: "group_members",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_stories_added_by_user_id",
                table: "group_stories",
                column: "added_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_stories_group_id",
                table: "group_stories",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_group_stories_story_id",
                table: "group_stories",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_groups_creator_id",
                table: "groups",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "ix_groups_group_name",
                table: "groups",
                column: "group_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_categories_category_name",
                table: "notification_categories",
                column: "category_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_types_display_name",
                table: "notification_types",
                column: "display_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_types_notification_category",
                table: "notification_types",
                column: "notification_category");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_notification_type_id",
                table: "notifications",
                column: "notification_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_user_id",
                table: "notifications",
                column: "recipient_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_source_user_id",
                table: "notifications",
                column: "source_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_poll_option_user_voters_id",
                table: "poll_option_user",
                column: "voters_id");

            migrationBuilder.CreateIndex(
                name: "ix_poll_options_poll_id_sort_order",
                table: "poll_options",
                columns: new[] { "poll_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_poll_options_poll_id_text",
                table: "poll_options",
                columns: new[] { "poll_id", "text" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_private_messages_conversation_id",
                table: "private_messages",
                column: "conversation_id");

            migrationBuilder.CreateIndex(
                name: "ix_private_messages_sender_user_id",
                table: "private_messages",
                column: "sender_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_profile_blog_posts_story_id",
                table: "profile_blog_posts",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_statuses_status_name",
                table: "recommendation_statuses",
                column: "status_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_successes_recommendation_id",
                table: "recommendation_successes",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_recommender_id",
                table: "recommendations",
                column: "recommender_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_status_id",
                table: "recommendations",
                column: "status_id");

            migrationBuilder.CreateIndex(
                name: "ix_recommendations_story_id",
                table: "recommendations",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_reasons_reason_name",
                table: "report_reasons",
                column: "reason_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_statuses_status_name",
                table: "report_statuses",
                column: "status_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reports_moderator_user_id",
                table: "reports",
                column: "moderator_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_report_reason_id",
                table: "reports",
                column: "report_reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_report_status_id",
                table: "reports",
                column: "report_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_user_id",
                table: "reports",
                column: "reporter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_tag_selection_entries_saved_tag_selection_id_tag_id",
                table: "saved_tag_selection_entries",
                columns: new[] { "saved_tag_selection_id", "tag_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_tag_selection_entries_tag_id",
                table: "saved_tag_selection_entries",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_tag_selections_user_id_nickname",
                table: "saved_tag_selections",
                columns: new[] { "user_id", "nickname" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_modes_name",
                table: "search_modes",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_series_author_id_name",
                table: "series",
                columns: new[] { "author_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_series_entries_story_id",
                table: "series_entries",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_setting_details_base_tag_id",
                table: "setting_details",
                column: "base_tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_setting_details_story_id",
                table: "setting_details",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_stories_author_id",
                table: "stories",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_stories_story_status_id",
                table: "stories",
                column: "story_status_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_acknowledgments_acknowledged_user_id",
                table: "story_acknowledgments",
                column: "acknowledged_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_acknowledgments_acknowledgment_role_id",
                table: "story_acknowledgments",
                column: "acknowledgment_role_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_arcs_story_id_sort_order",
                table: "story_arcs",
                columns: new[] { "story_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_story_arcs_story_id_title",
                table: "story_arcs",
                columns: new[] { "story_id", "title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_story_character_relationships_story_id",
                table: "story_character_relationships",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_character_story_character_relationship_story_characte",
                table: "story_character_story_character_relationship",
                column: "story_characters_story_character_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_characters_character_tag_id",
                table: "story_characters",
                column: "character_tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_characters_story_id",
                table: "story_characters",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_details_slug",
                table: "story_details",
                column: "slug",
                unique: true,
                filter: "\"slug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_story_imports_source_url",
                table: "story_imports",
                column: "source_url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_story_imports_story_id",
                table: "story_imports",
                column: "story_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_story_listing_search_vector",
                table: "story_listings",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_story_relationships_relationship_type_id",
                table: "story_relationships",
                column: "relationship_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_relationships_target_story_id",
                table: "story_relationships",
                column: "target_story_id");

            migrationBuilder.CreateIndex(
                name: "ix_story_tags_tag_id",
                table: "story_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_tag_types_type_name",
                table: "tag_types",
                column: "type_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tags_parent_tag_id",
                table: "tags",
                column: "parent_tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_tags_tag_name",
                table: "tags",
                column: "tag_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tags_tag_type_id",
                table: "tags",
                column: "tag_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_themes_name",
                table: "themes",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_badge_key",
                table: "user_badges",
                column: "badge_key");

            migrationBuilder.CreateIndex(
                name: "ix_user_chapter_interactions_chapter_id",
                table: "user_chapter_interactions",
                column: "chapter_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_custom_filters_search_mode_key",
                table: "user_custom_filters",
                column: "search_mode_key");

            migrationBuilder.CreateIndex(
                name: "ix_user_custom_filters_user_id",
                table: "user_custom_filters",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_interaction_filters_name",
                table: "user_interaction_filters",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_notification_settings_notification_type_id",
                table: "user_notification_settings",
                column: "notification_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_profile_comments_profile_user_id",
                table: "user_profile_comments",
                column: "profile_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_search_settings_interaction_filter_key",
                table: "user_search_settings",
                column: "interaction_filter_key");

            migrationBuilder.CreateIndex(
                name: "ix_user_search_settings_search_mode_key",
                table: "user_search_settings",
                column: "search_mode_key");

            migrationBuilder.CreateIndex(
                name: "ix_user_search_settings_user_id_search_mode_key_interaction_fi",
                table: "user_search_settings",
                columns: new[] { "user_id", "search_mode_key", "interaction_filter_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interactions_in_progress",
                table: "user_story_interactions",
                column: "user_id",
                filter: "\"is_in_progress\" = true")
                .Annotation("Npgsql:IndexInclude", new[] { "story_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interactions_recommendation_id",
                table: "user_story_interactions",
                column: "recommendation_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_story_interactions_story_id",
                table: "user_story_interactions",
                column: "story_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_story_recommendation_sources_source_recommendation_id",
                table: "user_story_recommendation_sources",
                column: "source_recommendation_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_tree_story_hidden_favorite",
                table: "user_story_tree_search_entries",
                column: "story_id",
                filter: "\"is_hidden_favorite\" = true")
                .Annotation("Npgsql:IndexInclude", new[] { "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_tree_user_hidden_favorite",
                table: "user_story_tree_search_entries",
                column: "user_id",
                filter: "\"is_hidden_favorite\" = true")
                .Annotation("Npgsql:IndexInclude", new[] { "story_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_base_comment_user_base_comments_liked_comments_comment_id",
                table: "base_comment_user",
                column: "liked_comments_comment_id",
                principalTable: "base_comments",
                principalColumn: "comment_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_base_comments_blog_post_comment_comment_id",
                table: "base_comments",
                column: "blog_post_comment_comment_id",
                principalTable: "blog_post_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_base_comments_chapter_comment_comment_id",
                table: "base_comments",
                column: "chapter_comment_comment_id",
                principalTable: "chapter_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_base_comments_group_comment_comment_id",
                table: "base_comments",
                column: "group_comment_comment_id",
                principalTable: "group_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_base_comments_base_comments_user_profile_comment_comment_id",
                table: "base_comments",
                column: "user_profile_comment_comment_id",
                principalTable: "user_profile_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "fk_chapter_comments_chapters_chapter_id",
                table: "chapter_comments",
                column: "chapter_id",
                principalTable: "chapters",
                principalColumn: "chapter_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_chapter_contents_chapters_chapter_id",
                table: "chapter_contents",
                column: "chapter_id",
                principalTable: "chapters",
                principalColumn: "chapter_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_base_blog_posts_asp_net_users_author_id",
                table: "base_blog_posts");

            migrationBuilder.DropForeignKey(
                name: "fk_base_comments_asp_net_users_user_id",
                table: "base_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_chapter_contents_users_author_id",
                table: "chapter_contents");

            migrationBuilder.DropForeignKey(
                name: "fk_groups_users_creator_id",
                table: "groups");

            migrationBuilder.DropForeignKey(
                name: "fk_stories_users_author_id",
                table: "stories");

            migrationBuilder.DropForeignKey(
                name: "fk_user_profile_comments_asp_net_users_profile_user_id",
                table: "user_profile_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_blog_post_comments_base_blog_posts_blog_post_id",
                table: "blog_post_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_blog_post_comments_base_comments_comment_id",
                table: "blog_post_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_chapter_comments_base_comments_comment_id",
                table: "chapter_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_group_comments_base_comments_comment_id",
                table: "group_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_user_profile_comments_base_comments_comment_id",
                table: "user_profile_comments");

            migrationBuilder.DropForeignKey(
                name: "fk_chapters_stories_story_id",
                table: "chapters");

            migrationBuilder.DropForeignKey(
                name: "fk_chapter_contents_chapters_chapter_id",
                table: "chapter_contents");

            migrationBuilder.DropTable(
                name: "also_favorited_scores");

            migrationBuilder.DropTable(
                name: "also_recommended_scores");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserPasskeys");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "base_blog_post_user");

            migrationBuilder.DropTable(
                name: "base_comment_user");

            migrationBuilder.DropTable(
                name: "beta_readers");

            migrationBuilder.DropTable(
                name: "blog_post_polls");

            migrationBuilder.DropTable(
                name: "co_authors");

            migrationBuilder.DropTable(
                name: "community_spotlights");

            migrationBuilder.DropTable(
                name: "conversation_participants");

            migrationBuilder.DropTable(
                name: "custom_list_entries");

            migrationBuilder.DropTable(
                name: "daily_story_stats");

            migrationBuilder.DropTable(
                name: "default_search_settings");

            migrationBuilder.DropTable(
                name: "feature_contributions");

            migrationBuilder.DropTable(
                name: "followed_users");

            migrationBuilder.DropTable(
                name: "group_blog_posts");

            migrationBuilder.DropTable(
                name: "group_folder_group_story");

            migrationBuilder.DropTable(
                name: "group_members");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "poll_option_user");

            migrationBuilder.DropTable(
                name: "private_messages");

            migrationBuilder.DropTable(
                name: "profile_blog_posts");

            migrationBuilder.DropTable(
                name: "recommendation_details");

            migrationBuilder.DropTable(
                name: "recommendation_successes");

            migrationBuilder.DropTable(
                name: "reports");

            migrationBuilder.DropTable(
                name: "saved_tag_selection_entries");

            migrationBuilder.DropTable(
                name: "series_entries");

            migrationBuilder.DropTable(
                name: "setting_details");

            migrationBuilder.DropTable(
                name: "site_daily_stats");

            migrationBuilder.DropTable(
                name: "site_polls");

            migrationBuilder.DropTable(
                name: "story_acknowledgments");

            migrationBuilder.DropTable(
                name: "story_arcs");

            migrationBuilder.DropTable(
                name: "story_character_story_character_relationship");

            migrationBuilder.DropTable(
                name: "story_details");

            migrationBuilder.DropTable(
                name: "story_imports");

            migrationBuilder.DropTable(
                name: "story_listings");

            migrationBuilder.DropTable(
                name: "story_relationships");

            migrationBuilder.DropTable(
                name: "story_tags");

            migrationBuilder.DropTable(
                name: "user_badges");

            migrationBuilder.DropTable(
                name: "user_chapter_interactions");

            migrationBuilder.DropTable(
                name: "user_custom_filters");

            migrationBuilder.DropTable(
                name: "user_notification_settings");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "user_search_settings");

            migrationBuilder.DropTable(
                name: "user_stats");

            migrationBuilder.DropTable(
                name: "user_story_interaction_dates");

            migrationBuilder.DropTable(
                name: "user_story_recommendation_sources");

            migrationBuilder.DropTable(
                name: "user_story_tree_search_entries");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "custom_lists");

            migrationBuilder.DropTable(
                name: "group_folders");

            migrationBuilder.DropTable(
                name: "group_stories");

            migrationBuilder.DropTable(
                name: "poll_options");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "report_reasons");

            migrationBuilder.DropTable(
                name: "report_statuses");

            migrationBuilder.DropTable(
                name: "saved_tag_selections");

            migrationBuilder.DropTable(
                name: "series");

            migrationBuilder.DropTable(
                name: "acknowledgment_roles");

            migrationBuilder.DropTable(
                name: "story_character_relationships");

            migrationBuilder.DropTable(
                name: "story_characters");

            migrationBuilder.DropTable(
                name: "story_relationship_types");

            migrationBuilder.DropTable(
                name: "badges");

            migrationBuilder.DropTable(
                name: "notification_types");

            migrationBuilder.DropTable(
                name: "search_modes");

            migrationBuilder.DropTable(
                name: "user_interaction_filters");

            migrationBuilder.DropTable(
                name: "user_story_interactions");

            migrationBuilder.DropTable(
                name: "base_polls");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "notification_categories");

            migrationBuilder.DropTable(
                name: "recommendations");

            migrationBuilder.DropTable(
                name: "tag_types");

            migrationBuilder.DropTable(
                name: "recommendation_statuses");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "themes");

            migrationBuilder.DropTable(
                name: "base_blog_posts");

            migrationBuilder.DropTable(
                name: "base_comments");

            migrationBuilder.DropTable(
                name: "blog_post_comments");

            migrationBuilder.DropTable(
                name: "chapter_comments");

            migrationBuilder.DropTable(
                name: "group_comments");

            migrationBuilder.DropTable(
                name: "user_profile_comments");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "stories");

            migrationBuilder.DropTable(
                name: "story_statuses");

            migrationBuilder.DropTable(
                name: "chapters");

            migrationBuilder.DropTable(
                name: "chapter_contents");
        }
    }
}
