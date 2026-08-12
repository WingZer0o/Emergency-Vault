using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EmergencyVault.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` build the model without going through Program.cs, which pulls secrets
/// from Infisical at startup. Only the schema is needed at design time, so the connection
/// string here is never actually opened.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=EmergencyVaultDb;TrustServerCertificate=True;");
        return new AppDbContext(optionsBuilder.Options);
    }
}
