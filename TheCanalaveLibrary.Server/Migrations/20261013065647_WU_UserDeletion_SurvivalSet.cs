using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// Owner ruling D13 (WU-UserDeletion, 2026-10-13): a deleted account's poll votes survive anonymized.
    /// <c>poll_votes</c> trades its composite <c>(poll_option_id, user_id)</c> PK for a surrogate
    /// <c>poll_vote_id</c> identity, <c>user_id</c> goes nullable with <c>ON DELETE SET NULL</c> (was
    /// CASCADE), and the old uniqueness moves to the unique index
    /// <c>ix_poll_votes_poll_option_id_user_id</c>, NULLS DISTINCT so anonymized rows coexist.
    /// <para>No data SQL in Up: Postgres numbers the existing rows when the identity column is added, and
    /// the old PK already guarantees the new index holds. Down deletes the anonymized votes first: they
    /// cannot exist under the old schema (a NULL cannot enter the composite PK, and the default the
    /// scaffold put on the NOT NULL change would have pointed them at a user id 0 the FK rejects).</para>
    /// </summary>
    public partial class WU_UserDeletion_SurvivalSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_poll_votes_users_user_id",
                table: "poll_votes");

            migrationBuilder.DropPrimaryKey(
                name: "pk_poll_votes",
                table: "poll_votes");

            migrationBuilder.AlterColumn<int>(
                name: "user_id",
                table: "poll_votes",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            // The scaffold's defaultValue: 0 is removed: an identity column takes its values from its
            // sequence, and Postgres numbers the existing rows when the column is added.
            migrationBuilder.AddColumn<int>(
                name: "poll_vote_id",
                table: "poll_votes",
                type: "integer",
                nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "pk_poll_votes",
                table: "poll_votes",
                column: "poll_vote_id");

            migrationBuilder.CreateIndex(
                name: "ix_poll_votes_poll_option_id_user_id",
                table: "poll_votes",
                columns: new[] { "poll_option_id", "user_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_poll_votes_users_user_id",
                table: "poll_votes",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Anonymized votes cannot exist under the old schema (see the class summary).
            migrationBuilder.Sql("DELETE FROM poll_votes WHERE user_id IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "fk_poll_votes_users_user_id",
                table: "poll_votes");

            migrationBuilder.DropPrimaryKey(
                name: "pk_poll_votes",
                table: "poll_votes");

            migrationBuilder.DropIndex(
                name: "ix_poll_votes_poll_option_id_user_id",
                table: "poll_votes");

            migrationBuilder.DropColumn(
                name: "poll_vote_id",
                table: "poll_votes");

            migrationBuilder.AlterColumn<int>(
                name: "user_id",
                table: "poll_votes",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_poll_votes",
                table: "poll_votes",
                columns: new[] { "poll_option_id", "user_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_poll_votes_users_user_id",
                table: "poll_votes",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
