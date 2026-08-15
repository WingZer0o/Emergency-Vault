using EmergencyVault.Domain.Entities;

namespace EmergencyVault.Application.Interfaces;

/// <summary>
/// EF Core-backed repository for the app-owned UserLoginHistory entity (successful-login audit history,
/// used to detect sign-ins from an IP the user hasn't used before).
/// </summary>
public interface IUserLoginHistoryRepository
{
    Task<bool> HasLoggedInFromIpAsync(Guid userId, string ipAddress, CancellationToken ct);
    Task AddAsync(UserLoginHistory entry, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
