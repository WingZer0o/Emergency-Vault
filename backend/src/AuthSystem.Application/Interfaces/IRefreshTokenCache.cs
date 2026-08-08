namespace AuthSystem.Application.Interfaces;

/// <summary>
/// Redis-backed fast-path cache for refresh token validation.
/// SQL Server (via IRefreshTokenRepository) remains the source of truth;
/// a cache miss always falls back to the database rather than failing the request.
/// </summary>
public interface IRefreshTokenCache
{
    Task SetAsync(string tokenHash, Guid userId, TimeSpan ttl, CancellationToken ct);
    Task<Guid?> GetUserIdAsync(string tokenHash, CancellationToken ct);
    Task RemoveAsync(string tokenHash, CancellationToken ct);
}
