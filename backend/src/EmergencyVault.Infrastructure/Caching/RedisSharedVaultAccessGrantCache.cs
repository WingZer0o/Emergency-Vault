using EmergencyVault.Application.Interfaces;
using StackExchange.Redis;

namespace EmergencyVault.Infrastructure.Caching;

public class RedisSharedVaultAccessGrantCache(IConnectionMultiplexer redis) : ISharedVaultAccessGrantCache
{
    private const string KeyPrefix = "shared-vault-access:";

    private static string BuildKey(Guid holderUserId, Guid ownerUserId) => $"{KeyPrefix}{holderUserId}:{ownerUserId}";

    public async Task GrantAsync(Guid holderUserId, Guid ownerUserId, TimeSpan ttl, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(BuildKey(holderUserId, ownerUserId), "1", ttl);
    }

    public async Task<bool> HasValidGrantAsync(Guid holderUserId, Guid ownerUserId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        return await db.KeyExistsAsync(BuildKey(holderUserId, ownerUserId));
    }
}
