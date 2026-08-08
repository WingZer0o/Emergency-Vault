using AuthSystem.Application.Interfaces;
using AuthSystem.Domain.Entities;
using AuthSystem.Infrastructure.Persistence;

namespace AuthSystem.Infrastructure.Repositories;

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
