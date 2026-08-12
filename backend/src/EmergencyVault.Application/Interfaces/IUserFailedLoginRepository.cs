using EmergencyVault.Domain.Entities;

namespace EmergencyVault.Application.Interfaces;

/// <summary>
/// EF Core-backed repository for the app-owned UserFailedLogin entity (login-attempt audit history).
/// </summary>
public interface IUserFailedLoginRepository
{
    Task AddAsync(UserFailedLogin attempt, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
