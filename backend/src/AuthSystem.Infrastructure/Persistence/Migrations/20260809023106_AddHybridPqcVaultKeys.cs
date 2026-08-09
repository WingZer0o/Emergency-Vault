using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHybridPqcVaultKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KeySchemeVersion",
                table: "VaultProfiles",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "MlKemPublicKey",
                table: "VaultProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MlKemSecretKeyWrappedByMasterKey",
                table: "VaultProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MlKemSecretKeyWrappedByRecoveryKey",
                table: "VaultProfiles",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KeySchemeVersion",
                table: "VaultProfiles");

            migrationBuilder.DropColumn(
                name: "MlKemPublicKey",
                table: "VaultProfiles");

            migrationBuilder.DropColumn(
                name: "MlKemSecretKeyWrappedByMasterKey",
                table: "VaultProfiles");

            migrationBuilder.DropColumn(
                name: "MlKemSecretKeyWrappedByRecoveryKey",
                table: "VaultProfiles");
        }
    }
}
