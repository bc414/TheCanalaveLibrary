using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class UserContentReveals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_content_reveals",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    entity_type = table.Column<short>(type: "smallint", nullable: false),
                    entity_id = table.Column<int>(type: "integer", nullable: false),
                    date_revealed = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_content_reveals", x => new { x.user_id, x.entity_type, x.entity_id });
                    table.ForeignKey(
                        name: "fk_user_content_reveals_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_content_reveals");
        }
    }
}
