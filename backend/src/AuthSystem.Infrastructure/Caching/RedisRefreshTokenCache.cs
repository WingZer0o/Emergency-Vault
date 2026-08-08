using AuthSystem.Application.Interfaces;
using StackExchange.Redis;

namespace AuthSystem.Infrastructure.Caching;

public class RedisRefreshTokenCache(IConnectionMultiplexer redis) : IRefreshTokenCache
{
    private const string KeyPrefix = "refresh:";

    private static string BuildKey(string tokenHash) => KeyPrefix + tokenHash;

    public async Task SetAsync(string tokenHash, Guid userId, TimeSpan ttl, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(BuildKey(tokenHash), userId.ToString(), ttl);
    }

    public async Task<Guid?> GetUserIdAsync(string tokenHash, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(BuildKey(tokenHash));
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return Guid.TryParse(value.ToString(), out var userId) ? userId : null;
    }

    public async Task RemoveAsync(string tokenHash, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(tokenHash));
    }
}
