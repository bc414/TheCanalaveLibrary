namespace TheCanalaveLibrary.PerfBaseline;

/// <summary>One SQL scenario: a named query shape lifted VERBATIM from a server service (the
/// file:line provenance is the R4 justification trail), parameterized over a deterministic pool
/// of hot ids resolved at run time.</summary>
public sealed record SqlScenario(
    string Name,
    string Provenance,
    string Sql,
    string ParameterPoolSql)
{
    /// <summary>Parameter name the pool values bind to (every scenario takes exactly one).</summary>
    public string ParameterName { get; init; } = "id";
}

public static class Scenarios
{
    /// <summary>
    /// The hot query shapes, matched 1:1 to real service code (ServerUserStoryInteractionReadService,
    /// ServerStoryReadService, ServerCommentReadService, ServerNotificationReadService,
    /// ServerTreeSearchReadService, ServerCoOccurrenceReadService). Global query filters
    /// (content rating, is_taken_down) are inlined where the read context would add them.
    /// Parameter pools pick the HOTTEST ids (most rows) deterministically — index wins show up
    /// at the hubs first, and hot ids make before/after runs comparable.
    /// </summary>
    public static readonly IReadOnlyList<SqlScenario> All =
    [
        new(
            "bookshelf_favorites_tab",
            "ServerUserStoryInteractionReadService.GetBookshelfStoryIdsAsync (Favorites)",
            """
            SELECT story_id FROM user_story_interactions
            WHERE user_id = @id AND is_favorite = true
            """,
            """
            SELECT user_id FROM user_story_interactions WHERE is_favorite = true
            GROUP BY user_id ORDER BY COUNT(*) DESC, user_id LIMIT 50
            """),

        new(
            "bookshelf_actively_reading_tab",
            "ServerUserStoryInteractionReadService.GetBookshelfStoryIdsAsync (ActivelyReading — composite booleans)",
            """
            SELECT story_id FROM user_story_interactions
            WHERE user_id = @id AND has_started = true AND is_completed = false AND is_ignored = false
            """,
            """
            SELECT user_id FROM user_story_interactions WHERE has_started = true
            GROUP BY user_id ORDER BY COUNT(*) DESC, user_id LIMIT 50
            """),

        new(
            "discover_date_published_page1",
            "ServerStoryReadService.GetListingsAsync (DatePublished sort) + read-context global filters",
            """
            SELECT s.story_id FROM stories s
            WHERE s.rating <= 1 AND s.is_taken_down = false
            ORDER BY s.published_date DESC LIMIT 20
            """,
            "SELECT 0"), // parameterless — pool is a dummy

        new(
            "discover_exclusion_probe_page1",
            "ServerStoryReadService.ApplyFilters interaction exclusion (NOT EXISTS) + DatePublished page",
            """
            SELECT s.story_id FROM stories s
            WHERE s.rating <= 1 AND s.is_taken_down = false
              AND NOT EXISTS (
                  SELECT 1 FROM user_story_interactions usi
                  WHERE usi.story_id = s.story_id AND usi.user_id = @id AND usi.is_ignored = true)
            ORDER BY s.published_date DESC LIMIT 20
            """,
            """
            SELECT user_id FROM user_story_interactions WHERE is_ignored = true
            GROUP BY user_id ORDER BY COUNT(*) DESC, user_id LIMIT 50
            """),

        new(
            "tag_filter_and_include_page1",
            "ServerStoryReadService.ApplyFilters tag include (AND mode, correlated EXISTS)",
            """
            SELECT s.story_id FROM stories s
            WHERE s.rating <= 1 AND s.is_taken_down = false
              AND EXISTS (SELECT 1 FROM story_tags st WHERE st.story_id = s.story_id AND st.tag_id = @id)
            ORDER BY s.published_date DESC LIMIT 20
            """,
            """
            SELECT tag_id FROM story_tags GROUP BY tag_id ORDER BY COUNT(*) DESC, tag_id LIMIT 20
            """),

        new(
            "comment_roots_page1",
            "ServerCommentReadService.GetChapterCommentsPageAsync roots page (chapter_id + parent NULL, date_posted DESC)",
            """
            SELECT cc.comment_id FROM chapter_comments cc
            JOIN base_comments bc ON bc.comment_id = cc.comment_id
            WHERE cc.chapter_id = @id AND bc.parent_comment_id IS NULL AND bc.is_taken_down = false
            ORDER BY cc.date_posted DESC LIMIT 20
            """,
            """
            SELECT chapter_id FROM chapter_comments GROUP BY chapter_id ORDER BY COUNT(*) DESC, chapter_id LIMIT 50
            """),

        new(
            "comment_roots_count",
            "ServerCommentReadService.GetChapterCommentsPageAsync roots count",
            """
            SELECT COUNT(*) FROM chapter_comments cc
            JOIN base_comments bc ON bc.comment_id = cc.comment_id
            WHERE cc.chapter_id = @id AND bc.parent_comment_id IS NULL AND bc.is_taken_down = false
            """,
            """
            SELECT chapter_id FROM chapter_comments GROUP BY chapter_id ORDER BY COUNT(*) DESC, chapter_id LIMIT 50
            """),

        new(
            "notifications_feed_newest_page1",
            "ServerNotificationReadService.GetNotificationsAsync (NewestFirst)",
            """
            SELECT notification_id FROM notifications
            WHERE recipient_user_id = @id
            ORDER BY date_created DESC, notification_id DESC LIMIT 20
            """,
            """
            SELECT recipient_user_id FROM notifications
            GROUP BY recipient_user_id ORDER BY COUNT(*) DESC, recipient_user_id LIMIT 50
            """),

        new(
            "notifications_unread_count",
            "ServerNotificationReadService.GetUnreadCountAsync",
            """
            SELECT COUNT(*) FROM notifications WHERE recipient_user_id = @id AND is_read = false
            """,
            """
            SELECT recipient_user_id FROM notifications
            GROUP BY recipient_user_id ORDER BY COUNT(*) DESC, recipient_user_id LIMIT 50
            """),

        new(
            "tree_search_wide_degree2",
            "ServerTreeSearchReadService.TraverseAsync (Favorite edges, MaxDegrees 2 — L8 traversal)",
            TreeSearchSql(maxDegrees: 2, edgeTypes: "1"),
            """
            SELECT story_id FROM user_story_tree_search_entries WHERE edge_type = 1
            GROUP BY story_id ORDER BY COUNT(*) DESC, story_id LIMIT 20
            """),

        new(
            "tree_search_deep_degree5",
            "ServerTreeSearchReadService.TraverseAsync (HiddenGem+AuthorSpotlight, MaxDegrees 5 — chain of trust)",
            TreeSearchSql(maxDegrees: 5, edgeTypes: "4,5"),
            """
            SELECT story_id FROM user_story_tree_search_entries WHERE edge_type IN (4,5)
            GROUP BY story_id ORDER BY COUNT(*) DESC, story_id LIMIT 20
            """),

        new(
            "also_favorited_top10",
            "ServerCoOccurrenceReadService.GetAlsoFavoritedAsync (ranked mart read)",
            """
            SELECT m.also_favorited_story_id, m.score
            FROM also_favorited_scores m
            JOIN stories s ON s.story_id = m.also_favorited_story_id
            WHERE m.story_id = @id AND s.is_taken_down = false AND s.story_status_id BETWEEN 2 AND 7
              AND s.rating <= 1
            ORDER BY m.score DESC, m.also_favorited_story_id LIMIT 10
            """,
            """
            SELECT story_id FROM also_favorited_scores GROUP BY story_id ORDER BY COUNT(*) DESC, story_id LIMIT 50
            """),
    ];

    /// <summary>The F59 rCTE, shape-identical to ServerTreeSearchReadService (anonymous viewer,
    /// no exclusions active — the traversal + presentation join are what the baseline times).</summary>
    private static string TreeSearchSql(int maxDegrees, string edgeTypes) => $"""
        WITH RECURSIVE traversal (is_story, node_id, degree) AS (
            SELECT true, @id, 0
            UNION ALL
            SELECT nxt.next_is_story, nxt.next_id, t.degree + 1
            FROM traversal t
            JOIN LATERAL (
                (SELECT e.user_id AS next_id, false AS next_is_story
                 FROM user_story_tree_search_entries e
                 WHERE t.is_story AND e.story_id = t.node_id AND e.edge_type IN ({edgeTypes})
                 LIMIT 50)
                UNION ALL
                (SELECT e2.story_id AS next_id, true AS next_is_story
                 FROM user_story_tree_search_entries e2
                 WHERE (NOT t.is_story) AND e2.user_id = t.node_id AND e2.edge_type IN ({edgeTypes})
                 LIMIT 50)
            ) nxt ON true
            WHERE t.degree < {maxDegrees}
        ) CYCLE is_story, node_id SET is_cycle USING path,
        hits AS (
            SELECT t.node_id AS story_id, MIN(t.degree) AS degree
            FROM traversal t WHERE t.is_story AND t.degree > 0
            GROUP BY t.node_id
        )
        SELECT h.story_id, h.degree FROM hits h
        JOIN stories s ON s.story_id = h.story_id
        WHERE s.is_taken_down = false AND s.story_status_id BETWEEN 2 AND 7 AND s.rating <= 1
          AND h.story_id <> @id
        ORDER BY h.degree ASC, random() LIMIT 100
        """;
}
