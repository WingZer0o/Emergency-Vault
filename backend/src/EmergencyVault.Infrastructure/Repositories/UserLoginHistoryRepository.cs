using EmergencyVault.Application.Interfaces;
using EmergencyVault.Domain.Entities;
using EmergencyVault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmergencyVault.Infrastructure.Repositories;

public class UserLoginHistoryRepository(AppDbContext db) : IUserLoginHistoryRepository
{
    public Task<bool> HasLoggedInFromIpAsync(Guid userId, string ipAddress, CancellationToken ct)
    {
        return db.UserLoginHistories.AnyAsync(h => h.UserId == userId && h.IpAddress == ipAddress, ct);
    }

    public async Task AddAsync(UserLoginHistory entry, CancellationToken ct)
    {
        await db.UserLoginHistories.AddAsync(entry, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
    }
}
