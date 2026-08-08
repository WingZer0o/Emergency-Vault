using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendUserFailedLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserFailedLogins_Users_UserId",
                table: "UserFailedLogins");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "UserFailedLogins",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "UserFailedLogins",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "UserFailedLogins",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_UserFailedLogins_Users_UserId",
                table: "UserFailedLogins",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserFailedLogins_Users_UserId",
                table: "UserFailedLogins");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "UserFailedLogins");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "UserFailedLogins");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "UserFailedLogins",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFailedLogins_Users_UserId",
                table: "UserFailedLogins",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
