using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuthSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultShareVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VerificationAttempts",
                table: "VaultKeyShares",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificationCodeExpiresAt",
                table: "VaultKeyShares",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificationCodeSentAt",
                table: "VaultKeyShares",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VerificationCounter",
                table: "VaultKeyShares",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "VerificationSecret",
                table: "VaultKeyShares",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerifiedAt",
                table: "VaultKeyShares",
                type: "datetimeoffset",
                nullable: true);

            // Existing shares (including every self-share row) predate the verification gate -
            // treat them as already verified so nothing currently accessible gets locked out.
            migrationBuilder.Sql(
                "UPDATE VaultKeyShares SET VerifiedAt = GrantedAt WHERE RevokedAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VerificationAttempts",
                table: "VaultKeyShares");

            migrationBuilder.DropColumn(
                name: "VerificationCodeExpiresAt",
                table: "VaultKeyShares");

            migrationBuilder.DropColumn(
                name: "VerificationCodeSentAt",
                table: "VaultKeyShares");

            migrationBuilder.DropColumn(
                name: "VerificationCounter",
                table: "VaultKeyShares");

            migrationBuilder.DropColumn(
                name: "VerificationSecret",
                table: "VaultKeyShares");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "VaultKeyShares");
        }
    }
}
