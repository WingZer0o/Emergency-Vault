using EmergencyVault.Application.Interfaces;
using EmergencyVault.Domain.Entities;
using EmergencyVault.Infrastructure.Persistence;

namespace EmergencyVault.Infrastructure.Repositories;

public class UserFailedLoginRepository(AppDbContext db) : IUserFailedLoginRepository
{
    public async Task AddAsync(UserFailedLogin attempt, CancellationToken ct)
    {
        await db.UserFailedLogins.AddAsync(attempt, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
    }
}
