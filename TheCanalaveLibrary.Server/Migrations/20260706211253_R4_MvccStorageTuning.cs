using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// R4 (MVCC storage tuning) — the Postgres-native levers the SQL-Server-era design never
    /// considered (writers-don't-block-readers made the old locking rationale moot; the real
    /// Postgres costs are dead-tuple bloat and vacuum pressure on high-churn tables):
    ///
    /// <list type="bullet">
    ///   <item><c>user_chapter_interactions</c> — now the highest-UPDATE table (every active
    ///   reader's row, every buffer flush). Its hot-updated columns (read_progress,
    ///   last_interaction_date, is_read) are NOT indexed, so updates are HOT-eligible:
    ///   <c>fillfactor 90</c> reserves same-page room for HOT chains (no index maintenance,
    ///   no index-referenced dead tuples).</item>
    ///   <item><c>daily_story_stats</c> — today's row per story is re-updated every view flush;
    ///   view_count is unindexed → HOT-eligible → same fillfactor treatment.</item>
    ///   <item><c>user_story_interactions</c> — flag toggles always change a partial-index
    ///   predicate column, so HOT is structurally defeated (fillfactor would only waste space);
    ///   aggressive autovacuum is the available lever for its churn.</item>
    /// </list>
    ///
    /// <c>autovacuum_vacuum_scale_factor 0.05</c> (vs default 0.20) vacuums at 5% dead rows —
    /// keeps bloat bounded and the visibility map fresh, which is also what the seven USI
    /// partial covering indexes need for index-only scans to actually skip the heap.
    /// (Index audit, same requirement: all 7 USI partial indexes are justified — one per
    /// bookshelf tab / profile favorites / discovery-exclusion probes — none dropped.)
    ///
    /// fillfactor applies to future page writes; no table rewrite is forced here.
    /// </summary>
    public partial class R4_MvccStorageTuning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE user_chapter_interactions
                    SET (fillfactor = 90, autovacuum_vacuum_scale_factor = 0.05);

                ALTER TABLE daily_story_stats
                    SET (fillfactor = 90, autovacuum_vacuum_scale_factor = 0.05);

                ALTER TABLE user_story_interactions
                    SET (autovacuum_vacuum_scale_factor = 0.05);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE user_chapter_interactions
                    RESET (fillfactor, autovacuum_vacuum_scale_factor);

                ALTER TABLE daily_story_stats
                    RESET (fillfactor, autovacuum_vacuum_scale_factor);

                ALTER TABLE user_story_interactions
                    RESET (autovacuum_vacuum_scale_factor);
                """);
        }
    }
}
