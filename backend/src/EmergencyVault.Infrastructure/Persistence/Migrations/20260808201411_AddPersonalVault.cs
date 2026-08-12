using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmergencyVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalVault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VaultItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CiphertextBlob = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaultItems_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaultKeyShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VaultOwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HolderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WrappedUvk = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultKeyShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaultKeyShares_Users_HolderUserId",
                        column: x => x.HolderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VaultKeyShares_Users_VaultOwnerUserId",
                        column: x => x.VaultOwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaultProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PublicKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KdfSalt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KdfIterations = table.Column<int>(type: "int", nullable: false),
                    PrivateKeyWrappedByMasterKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrivateKeyWrappedByRecoveryKey = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaultProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VaultItems_OwnerUserId",
                table: "VaultItems",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultKeyShares_HolderUserId",
                table: "VaultKeyShares",
                column: "HolderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultKeyShares_VaultOwnerUserId_HolderUserId",
                table: "VaultKeyShares",
                columns: new[] { "VaultOwnerUserId", "HolderUserId" },
                unique: true,
                filter: "[RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VaultProfiles_UserId",
                table: "VaultProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VaultItems");

            migrationBuilder.DropTable(
                name: "VaultKeyShares");

            migrationBuilder.DropTable(
                name: "VaultProfiles");
        }
    }
}
