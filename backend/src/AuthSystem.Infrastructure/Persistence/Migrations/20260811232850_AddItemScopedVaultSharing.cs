using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemScopedVaultSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VaultKeyShares_VaultOwnerUserId_HolderUserId",
                table: "VaultKeyShares");

            migrationBuilder.RenameColumn(
                name: "WrappedUvk",
                table: "VaultKeyShares",
                newName: "WrappedKey");

            migrationBuilder.AddColumn<bool>(
                name: "CanEdit",
                table: "VaultKeyShares",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "VaultItemId",
                table: "VaultKeyShares",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WrappedItemKey",
                table: "VaultItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VaultKeyShares_Owner_Holder_Item",
                table: "VaultKeyShares",
                columns: new[] { "VaultOwnerUserId", "HolderUserId", "VaultItemId" },
                unique: true,
                filter: "[RevokedAt] IS NULL AND [VaultItemId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VaultKeyShares_Owner_Holder_WholeVault",
                table: "VaultKeyShares",
                columns: new[] { "VaultOwnerUserId", "HolderUserId" },
                unique: true,
                filter: "[RevokedAt] IS NULL AND [VaultItemId] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VaultKeyShares_VaultItemId",
                table: "VaultKeyShares",
                column: "VaultItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_VaultKeyShares_VaultItems_VaultItemId",
                table: "VaultKeyShares",
                column: "VaultItemId",
                principalTable: "VaultItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VaultKeyShares_VaultItems_VaultItemId",
                table: "VaultKeyShares");

            migrationBuilder.DropIndex(
                name: "IX_VaultKeyShares_Owner_Holder_Item",
                table: "VaultKeyShares");

            migrationBuilder.DropIndex(
                name: "IX_VaultKeyShares_Owner_Holder_WholeVault",
                table: "VaultKeyShares");

            migrationBuilder.DropIndex(
                name: "IX_VaultKeyShares_VaultItemId",
                table: "VaultKeyShares");

            migrationBuilder.DropColumn(
                name: "CanEdit",
                table: "VaultKeyShares");

            migrationBuilder.DropColumn(
                name: "VaultItemId",
                table: "VaultKeyShares");

            migrationBuilder.DropColumn(
                name: "WrappedItemKey",
                table: "VaultItems");

            migrationBuilder.RenameColumn(
                name: "WrappedKey",
                table: "VaultKeyShares",
                newName: "WrappedUvk");

            migrationBuilder.CreateIndex(
                name: "IX_VaultKeyShares_VaultOwnerUserId_HolderUserId",
                table: "VaultKeyShares",
                columns: new[] { "VaultOwnerUserId", "HolderUserId" },
                unique: true,
                filter: "[RevokedAt] IS NULL");
        }
    }
}
