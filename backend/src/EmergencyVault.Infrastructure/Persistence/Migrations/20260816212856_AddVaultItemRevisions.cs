using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmergencyVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultItemRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VaultItemRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VaultItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreviousCiphertextBlob = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultItemRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaultItemRevisions_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VaultItemRevisions_VaultItems_VaultItemId",
                        column: x => x.VaultItemId,
                        principalTable: "VaultItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VaultItemRevisions_ChangedByUserId",
                table: "VaultItemRevisions",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultItemRevisions_VaultItemId",
                table: "VaultItemRevisions",
                column: "VaultItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VaultItemRevisions");
        }
    }
}
