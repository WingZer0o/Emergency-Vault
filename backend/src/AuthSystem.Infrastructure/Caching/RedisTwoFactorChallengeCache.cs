using System.Text.Json;
using AuthSystem.Application.Interfaces;
using StackExchange.Redis;

namespace AuthSystem.Infrastructure.Caching;

public class RedisTwoFactorChallengeCache(IConnectionMultiplexer redis) : ITwoFactorChallengeCache
{
    private const string KeyPrefix = "2fa-challenge:";

    private static string BuildKey(string challengeToken) => KeyPrefix + challengeToken;

    public async Task SetAsync(string challengeToken, TwoFactorChallenge challenge, TimeSpan ttl, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(BuildKey(challengeToken), JsonSerializer.Serialize(challenge), ttl);
    }

    public async Task<TwoFactorChallenge?> GetAsync(string challengeToken, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(BuildKey(challengeToken));
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize<TwoFactorChallenge>(value.ToString());
    }

    public async Task RemoveAsync(string challengeToken, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(challengeToken));
    }
}
