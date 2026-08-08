using AuthSystem.Domain.Entities;

namespace AuthSystem.Application.Interfaces;

/// <summary>
/// EF Core-backed repository for the app-owned RefreshToken entity (the source of truth).
/// Identity's own User/Role persistence is already handled by UserManager/RoleManager,
/// which are Microsoft's repository abstraction over EF Core - no need to wrap those again.
/// </summary>
public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
