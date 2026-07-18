using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheCanalaveLibrary.Server.Migrations
{
    /// <inheritdoc />
    public partial class MA102_DropPhantomUserRolesShadowFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_asp_net_roles_asp_net_users_user_id",
                table: "AspNetRoles");

            migrationBuilder.DropIndex(
                name: "ix_asp_net_roles_user_id",
                table: "AspNetRoles");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "AspNetRoles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "AspNetRoles",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "id",
                keyValue: 1,
                column: "user_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "id",
                keyValue: 2,
                column: "user_id",
                value: null);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "id",
                keyValue: 3,
                column: "user_id",
                value: null);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_roles_user_id",
                table: "AspNetRoles",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_roles_asp_net_users_user_id",
                table: "AspNetRoles",
                column: "user_id",
                principalTable: "AspNetUsers",
                principalColumn: "id");
        }
    }
}
