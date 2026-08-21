using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmergencyVault.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTotpTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TotpSecret",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TwoFactorMethod",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill: users who already had email 2FA on (TwoFactorEnabled=1) keep working under
            // the new method-based model instead of silently losing their second factor.
            migrationBuilder.Sql("UPDATE [Users] SET [TwoFactorMethod] = 1 WHERE [TwoFactorEnabled] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TotpSecret",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwoFactorMethod",
                table: "Users");
        }
    }
}
