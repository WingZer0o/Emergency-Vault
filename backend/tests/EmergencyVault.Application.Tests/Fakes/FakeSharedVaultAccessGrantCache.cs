using EmergencyVault.Application.Interfaces;

namespace EmergencyVault.Application.Tests.Fakes;

/// <summary>In-memory stand-in for the Redis-backed per-session access grant cache. Ignores ttl - no test needs expiry.</summary>
public class FakeSharedVaultAccessGrantCache : ISharedVaultAccessGrantCache
{
    private readonly HashSet<(Guid HolderUserId, Guid OwnerUserId)> _grants = [];

    public Task GrantAsync(Guid holderUserId, Guid ownerUserId, TimeSpan ttl, CancellationToken ct)
    {
        _grants.Add((holderUserId, ownerUserId));
        return Task.CompletedTask;
    }

    public Task<bool> HasValidGrantAsync(Guid holderUserId, Guid ownerUserId, CancellationToken ct) =>
        Task.FromResult(_grants.Contains((holderUserId, ownerUserId)));
}
