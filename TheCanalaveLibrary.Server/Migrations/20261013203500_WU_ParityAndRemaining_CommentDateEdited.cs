using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <summary>
    /// WU-ParityAndRemaining (2026-10-13; service audit §2.12, <c>EditCommentAsync</c>): the comment
    /// edited marker. <c>base_comments.date_edited</c> is nullable and NULL means "never edited", which
    /// is true of every existing row, so no backfill. <c>EditCommentAsync</c> stamps it only on a real
    /// change of text or spoiler flag.
    /// </summary>
    public partial class WU_ParityAndRemaining_CommentDateEdited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "date_edited",
                table: "base_comments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "date_edited",
                table: "base_comments");
        }
    }
}
