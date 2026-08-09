using System.Text.Json;
using AuthSystem.Application.Interfaces;
using StackExchange.Redis;

namespace AuthSystem.Infrastructure.Caching;

public class RedisSharedVaultAccessChallengeCache(IConnectionMultiplexer redis) : ISharedVaultAccessChallengeCache
{
    private const string KeyPrefix = "shared-vault-challenge:";

    private static string BuildKey(string challengeToken) => KeyPrefix + challengeToken;

    public async Task SetAsync(string challengeToken, SharedVaultAccessChallenge challenge, TimeSpan ttl, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(BuildKey(challengeToken), JsonSerializer.Serialize(challenge), ttl);
    }

    public async Task<SharedVaultAccessChallenge?> GetAsync(string challengeToken, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(BuildKey(challengeToken));
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<SharedVaultAccessChallenge>(value.ToString());
    }

    public async Task RemoveAsync(string challengeToken, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(challengeToken));
    }
}
