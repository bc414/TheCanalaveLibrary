using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// Owner ruling D23 (WU-PollVoteIntegrity, 2026-10-13): single-choice is a database constraint.
    /// <c>poll_votes</c> gains <c>poll_id</c> (composite FK <c>(poll_id, poll_option_id)</c> →
    /// <c>poll_options</c>, replacing the single-column option FK) and <c>allow_multiple</c> (composite
    /// FK <c>(poll_id, allow_multiple)</c> → <c>base_polls</c>, no ON UPDATE action — the
    /// <c>ConfigLocked</c> rider), plus the STORED generated guard <c>single_choice_poll_id</c>, unique
    /// with <c>user_id</c> (NULLS DISTINCT). The two alternate keys are the FK targets.
    /// <para><b>Data SQL in Up.</b> The new columns are added nullable, backfilled from each vote's option
    /// and poll, then made NOT NULL — no scaffold default is left on them. Before the guard index, a live
    /// voter holding several rows on one single-choice poll (the race this migration closes) keeps only
    /// the newest row (highest <c>poll_vote_id</c>, the last write under the replace semantics); the data
    /// cannot say which row was the "real" vote, so the choice is a convention. Anonymized rows
    /// (<c>user_id</c> NULL) are untouched: the guard is NULLS DISTINCT. Down() restores the
    /// single-column FK and drops the rest; the de-duplication is one-way.</para>
    /// </summary>
    public partial class WU_PollVoteIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_poll_votes_poll_options_poll_option_id",
                table: "poll_votes");

            // Added nullable, backfilled, then NOT NULL (the scaffold's DEFAULT 0 / false would have stayed
            // on the columns).
            migrationBuilder.AddColumn<bool>(
                name: "allow_multiple",
                table: "poll_votes",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "poll_id",
                table: "poll_votes",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE poll_votes AS v
                SET poll_id = o.poll_id, allow_multiple = p.allow_multiple
                FROM poll_options AS o
                JOIN base_polls AS p ON p.poll_id = o.poll_id
                WHERE o.poll_option_id = v.poll_option_id;
                """);

            // One live vote per (single-choice poll, voter) before the guard index: keep the newest.
            migrationBuilder.Sql("""
                DELETE FROM poll_votes AS v
                USING poll_votes AS w
                WHERE NOT v.allow_multiple
                  AND v.user_id IS NOT NULL
                  AND w.poll_id = v.poll_id
                  AND w.user_id = v.user_id
                  AND w.poll_vote_id > v.poll_vote_id;
                """);

            migrationBuilder.AlterColumn<bool>(
                name: "allow_multiple",
                table: "poll_votes",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "poll_id",
                table: "poll_votes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "single_choice_poll_id",
                table: "poll_votes",
                type: "integer",
                nullable: true,
                computedColumnSql: "CASE WHEN allow_multiple THEN NULL ELSE poll_id END",
                stored: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_poll_options_poll_id_poll_option_id",
                table: "poll_options",
                columns: new[] { "poll_id", "poll_option_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ak_base_polls_poll_id_allow_multiple",
                table: "base_polls",
                columns: new[] { "poll_id", "allow_multiple" });

            migrationBuilder.CreateIndex(
                name: "ix_poll_votes_poll_id_allow_multiple",
                table: "poll_votes",
                columns: new[] { "poll_id", "allow_multiple" });

            migrationBuilder.CreateIndex(
                name: "ix_poll_votes_poll_id_poll_option_id",
                table: "poll_votes",
                columns: new[] { "poll_id", "poll_option_id" });

            migrationBuilder.CreateIndex(
                name: "ix_poll_votes_single_choice_poll_id_user_id",
                table: "poll_votes",
                columns: new[] { "single_choice_poll_id", "user_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_poll_votes_base_polls_poll_id_allow_multiple",
                table: "poll_votes",
                columns: new[] { "poll_id", "allow_multiple" },
                principalTable: "base_polls",
                principalColumns: new[] { "poll_id", "allow_multiple" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_poll_votes_poll_options_poll_id_poll_option_id",
                table: "poll_votes",
                columns: new[] { "poll_id", "poll_option_id" },
                principalTable: "poll_options",
                principalColumns: new[] { "poll_id", "poll_option_id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_poll_votes_base_polls_poll_id_allow_multiple",
                table: "poll_votes");

            migrationBuilder.DropForeignKey(
                name: "fk_poll_votes_poll_options_poll_id_poll_option_id",
                table: "poll_votes");

            migrationBuilder.DropIndex(
                name: "ix_poll_votes_poll_id_allow_multiple",
                table: "poll_votes");

            migrationBuilder.DropIndex(
                name: "ix_poll_votes_poll_id_poll_option_id",
                table: "poll_votes");

            migrationBuilder.DropIndex(
                name: "ix_poll_votes_single_choice_poll_id_user_id",
                table: "poll_votes");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_poll_options_poll_id_poll_option_id",
                table: "poll_options");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_base_polls_poll_id_allow_multiple",
                table: "base_polls");

            migrationBuilder.DropColumn(
                name: "single_choice_poll_id",
                table: "poll_votes");

            migrationBuilder.DropColumn(
                name: "allow_multiple",
                table: "poll_votes");

            migrationBuilder.DropColumn(
                name: "poll_id",
                table: "poll_votes");

            migrationBuilder.AddForeignKey(
                name: "fk_poll_votes_poll_options_poll_option_id",
                table: "poll_votes",
                column: "poll_option_id",
                principalTable: "poll_options",
                principalColumn: "poll_option_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
