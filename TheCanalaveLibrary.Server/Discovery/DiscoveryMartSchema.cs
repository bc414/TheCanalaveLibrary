namespace TheCanalaveLibrary.Server;

/// <summary>
/// Raw SQL for the three discovery mart tables (layer8-data-marts.md). These tables have NO EF
/// model, DbSet, or migration — they are created and rebuilt here, by the L8 workers, only.
///
/// <para><b>Rebuild pattern (fresh-staging swap):</b> each rebuild drops and recreates the
/// staging table, bulk-INSERTs the build query, indexes it, then atomically (one transaction)
/// drops the live table, renames staging→live, and renames the PK + indexes to their canonical
/// live names. The rename step is load-bearing: PostgreSQL index/constraint-backing-index names
/// are schema-wide and do NOT rename when their table is renamed, so without it the next
/// rebuild's <c>CREATE INDEX …_staging</c> would collide with the index now living on the live
/// table. Readers never observe a missing table (DDL is transactional; they briefly queue on
/// the ACCESS EXCLUSIVE lock).</para>
///
/// <para><b>Build predicates (AD4):</b> visible stories only (not taken down, approved statuses
/// InProgress..OpenBeta); approved, non-taken-down, non-anonymized recommendations; hidden
/// favorites become plain Favorite edges iff the EDGE OWNER opted in
/// (<c>allow_discovery_from_hidden_favorites</c>). IDs only — rating/display stay on the story
/// tables and are joined at presentation time.</para>
/// </summary>
public static class DiscoveryMartSchema
{
    /// <summary>Visible-story predicate fragment (alias <c>s</c>): published, approved, not taken down.
    /// StoryStatusEnum InProgress=2 .. OpenBeta=7 (Draft/PendingApproval/Rejected excluded).</summary>
    public const string VisibleStory = "s.is_taken_down = false AND s.story_status_id BETWEEN 2 AND 7";

    /// <summary>Discovery-eligible recommendation predicate fragment (alias <c>r</c>):
    /// approved (status 2), not taken down, not anonymized.</summary>
    public const string EligibleRecommendation =
        "r.is_taken_down = false AND r.status_id = 2 AND r.recommender_id IS NOT NULL";

    // ── Tree-search edge list ────────────────────────────────────────────────────────────────

    public const string TreeSearchTable = "user_story_tree_search_entries";

    /// <summary>Idempotent live-table bootstrap so consumers never hit 42P01 before the first
    /// rebuild (an empty mart is a valid state; a missing one is not).</summary>
    public const string TreeSearchEnsureLive = $"""
        CREATE TABLE IF NOT EXISTS {TreeSearchTable} (
            user_id   integer  NOT NULL,
            story_id  integer  NOT NULL,
            edge_type smallint NOT NULL,
            CONSTRAINT pk_{TreeSearchTable} PRIMARY KEY (user_id, story_id, edge_type));
        CREATE INDEX IF NOT EXISTS ix_tree_search_user_edge
            ON {TreeSearchTable} (user_id, edge_type) INCLUDE (story_id);
        CREATE INDEX IF NOT EXISTS ix_tree_search_story_edge
            ON {TreeSearchTable} (story_id, edge_type) INCLUDE (user_id);
        """;

    public const string TreeSearchCreateStaging = $"""
        DROP TABLE IF EXISTS {TreeSearchTable}_staging;
        CREATE TABLE {TreeSearchTable}_staging (
            user_id   integer  NOT NULL,
            story_id  integer  NOT NULL,
            edge_type smallint NOT NULL,
            CONSTRAINT pk_{TreeSearchTable}_staging PRIMARY KEY (user_id, story_id, edge_type));
        """;

    /// <summary>The six edge projections (TreeSearchEdgeType values 0–5). UNION ALL is safe:
    /// each arm is duplicate-free by construction (unique source rows) and arms differ by edge_type.</summary>
    public const string TreeSearchBuild = $"""
        INSERT INTO {TreeSearchTable}_staging (user_id, story_id, edge_type)
        -- 0 AuthoredBy
        SELECT s.author_id, s.story_id, 0::smallint
        FROM stories s
        WHERE s.author_id IS NOT NULL AND {VisibleStory}
        UNION ALL
        -- 1 Favorite: public favorites + edge-owner-consented hidden favorites (no separate flag)
        SELECT usi.user_id, usi.story_id, 1::smallint
        FROM user_story_interactions usi
        JOIN stories s ON s.story_id = usi.story_id AND {VisibleStory}
        JOIN "AspNetUsers" u ON u.id = usi.user_id
        WHERE usi.is_favorite = true
           OR (usi.is_hidden_favorite = true AND u.allow_discovery_from_hidden_favorites = true)
        UNION ALL
        -- 2 Recommendation
        SELECT r.recommender_id, r.story_id, 2::smallint
        FROM recommendations r
        JOIN stories s ON s.story_id = r.story_id AND {VisibleStory}
        WHERE {EligibleRecommendation}
        UNION ALL
        -- 3 Vouch projection: voucher → each published story authored by the vouchee
        SELECT v.vouching_user_id, s.story_id, 3::smallint
        FROM vouches v
        JOIN stories s ON s.author_id = v.vouched_user_id AND {VisibleStory}
        UNION ALL
        -- 4 HiddenGem (≤5/user, service-enforced at write time)
        SELECT r.recommender_id, r.story_id, 4::smallint
        FROM recommendations r
        JOIN stories s ON s.story_id = r.story_id AND {VisibleStory}
        WHERE {EligibleRecommendation} AND r.is_hidden_gem = true
        UNION ALL
        -- 5 AuthorSpotlight (≤5/story; lands on the (recommender, story) pair)
        SELECT r.recommender_id, r.story_id, 5::smallint
        FROM recommendations r
        JOIN stories s ON s.story_id = r.story_id AND {VisibleStory}
        WHERE {EligibleRecommendation} AND r.is_highlighted_by_author = true;
        """;

    public const string TreeSearchIndexStaging = $"""
        CREATE INDEX ix_tree_search_user_edge_staging
            ON {TreeSearchTable}_staging (user_id, edge_type) INCLUDE (story_id);
        CREATE INDEX ix_tree_search_story_edge_staging
            ON {TreeSearchTable}_staging (story_id, edge_type) INCLUDE (user_id);
        """;

    public const string TreeSearchSwap = $"""
        BEGIN;
        DROP TABLE IF EXISTS {TreeSearchTable};
        ALTER TABLE {TreeSearchTable}_staging RENAME TO {TreeSearchTable};
        ALTER TABLE {TreeSearchTable} RENAME CONSTRAINT pk_{TreeSearchTable}_staging TO pk_{TreeSearchTable};
        ALTER INDEX ix_tree_search_user_edge_staging RENAME TO ix_tree_search_user_edge;
        ALTER INDEX ix_tree_search_story_edge_staging RENAME TO ix_tree_search_story_edge;
        COMMIT;
        """;

    // ── Co-occurrence score marts (story→story results, a different shape from the edge list) ─

    public const string AlsoFavoritedTable = "also_favorited_scores";
    public const string AlsoRecommendedTable = "also_recommended_scores";

    public const string AlsoFavoritedEnsureLive = $"""
        CREATE TABLE IF NOT EXISTS {AlsoFavoritedTable} (
            story_id                integer NOT NULL,
            also_favorited_story_id integer NOT NULL,
            score                   integer NOT NULL,
            CONSTRAINT pk_{AlsoFavoritedTable} PRIMARY KEY (story_id, also_favorited_story_id));
        CREATE INDEX IF NOT EXISTS ix_also_favorited_scores_story_score
            ON {AlsoFavoritedTable} (story_id, score DESC) INCLUDE (also_favorited_story_id);
        """;

    public const string AlsoFavoritedCreateStaging = $"""
        DROP TABLE IF EXISTS {AlsoFavoritedTable}_staging;
        CREATE TABLE {AlsoFavoritedTable}_staging (
            story_id                integer NOT NULL,
            also_favorited_story_id integer NOT NULL,
            score                   integer NOT NULL,
            CONSTRAINT pk_{AlsoFavoritedTable}_staging PRIMARY KEY (story_id, also_favorited_story_id));
        """;

    /// <summary>Self-join on the favorite signal (public + consented hidden — the SAME edge-owner
    /// consent rule as the tree mart, spec §5.7); count overlapping users per ordered story pair.
    /// The symmetric join emits both directions naturally.</summary>
    public const string AlsoFavoritedBuild = $"""
        WITH fav AS (
            SELECT usi.user_id, usi.story_id
            FROM user_story_interactions usi
            JOIN stories s ON s.story_id = usi.story_id AND {VisibleStory}
            JOIN "AspNetUsers" u ON u.id = usi.user_id
            WHERE usi.is_favorite = true
               OR (usi.is_hidden_favorite = true AND u.allow_discovery_from_hidden_favorites = true)
        )
        INSERT INTO {AlsoFavoritedTable}_staging (story_id, also_favorited_story_id, score)
        SELECT a.story_id, b.story_id, COUNT(*)::integer
        FROM fav a
        JOIN fav b ON b.user_id = a.user_id AND b.story_id <> a.story_id
        GROUP BY a.story_id, b.story_id;
        """;

    public const string AlsoFavoritedIndexStaging = $"""
        CREATE INDEX ix_also_favorited_scores_story_score_staging
            ON {AlsoFavoritedTable}_staging (story_id, score DESC) INCLUDE (also_favorited_story_id);
        """;

    public const string AlsoFavoritedSwap = $"""
        BEGIN;
        DROP TABLE IF EXISTS {AlsoFavoritedTable};
        ALTER TABLE {AlsoFavoritedTable}_staging RENAME TO {AlsoFavoritedTable};
        ALTER TABLE {AlsoFavoritedTable} RENAME CONSTRAINT pk_{AlsoFavoritedTable}_staging TO pk_{AlsoFavoritedTable};
        ALTER INDEX ix_also_favorited_scores_story_score_staging RENAME TO ix_also_favorited_scores_story_score;
        COMMIT;
        """;

    public const string AlsoRecommendedEnsureLive = $"""
        CREATE TABLE IF NOT EXISTS {AlsoRecommendedTable} (
            story_id                  integer NOT NULL,
            also_recommended_story_id integer NOT NULL,
            score                     integer NOT NULL,
            CONSTRAINT pk_{AlsoRecommendedTable} PRIMARY KEY (story_id, also_recommended_story_id));
        CREATE INDEX IF NOT EXISTS ix_also_recommended_scores_story_score
            ON {AlsoRecommendedTable} (story_id, score DESC) INCLUDE (also_recommended_story_id);
        """;

    public const string AlsoRecommendedCreateStaging = $"""
        DROP TABLE IF EXISTS {AlsoRecommendedTable}_staging;
        CREATE TABLE {AlsoRecommendedTable}_staging (
            story_id                  integer NOT NULL,
            also_recommended_story_id integer NOT NULL,
            score                     integer NOT NULL,
            CONSTRAINT pk_{AlsoRecommendedTable}_staging PRIMARY KEY (story_id, also_recommended_story_id));
        """;

    /// <summary>Mirror of the favorites build on eligible recommendations (anonymized excluded).</summary>
    public const string AlsoRecommendedBuild = $"""
        WITH rec AS (
            SELECT r.recommender_id AS user_id, r.story_id
            FROM recommendations r
            JOIN stories s ON s.story_id = r.story_id AND {VisibleStory}
            WHERE {EligibleRecommendation}
        )
        INSERT INTO {AlsoRecommendedTable}_staging (story_id, also_recommended_story_id, score)
        SELECT a.story_id, b.story_id, COUNT(*)::integer
        FROM rec a
        JOIN rec b ON b.user_id = a.user_id AND b.story_id <> a.story_id
        GROUP BY a.story_id, b.story_id;
        """;

    public const string AlsoRecommendedIndexStaging = $"""
        CREATE INDEX ix_also_recommended_scores_story_score_staging
            ON {AlsoRecommendedTable}_staging (story_id, score DESC) INCLUDE (also_recommended_story_id);
        """;

    public const string AlsoRecommendedSwap = $"""
        BEGIN;
        DROP TABLE IF EXISTS {AlsoRecommendedTable};
        ALTER TABLE {AlsoRecommendedTable}_staging RENAME TO {AlsoRecommendedTable};
        ALTER TABLE {AlsoRecommendedTable} RENAME CONSTRAINT pk_{AlsoRecommendedTable}_staging TO pk_{AlsoRecommendedTable};
        ALTER INDEX ix_also_recommended_scores_story_score_staging RENAME TO ix_also_recommended_scores_story_score;
        COMMIT;
        """;
}
