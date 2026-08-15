using EmergencyVault.Application.Interfaces;

namespace EmergencyVault.Application.Tests.Fakes;

/// <summary>In-memory stand-in for the Redis-backed per-view access challenge cache. Ignores ttl - no test needs expiry.</summary>
public class FakeSharedVaultAccessChallengeCache : ISharedVaultAccessChallengeCache
{
    private readonly Dictionary<string, SharedVaultAccessChallenge> _challenges = [];

    public Task SetAsync(string challengeToken, SharedVaultAccessChallenge challenge, TimeSpan ttl, CancellationToken ct)
    {
        _challenges[challengeToken] = challenge;
        return Task.CompletedTask;
    }

    public Task<SharedVaultAccessChallenge?> GetAsync(string challengeToken, CancellationToken ct) =>
        Task.FromResult(_challenges.GetValueOrDefault(challengeToken));

    public Task RemoveAsync(string challengeToken, CancellationToken ct)
    {
        _challenges.Remove(challengeToken);
        return Task.CompletedTask;
    }
}
