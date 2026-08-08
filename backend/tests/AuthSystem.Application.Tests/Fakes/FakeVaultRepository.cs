using AuthSystem.Application.Interfaces;
using AuthSystem.Domain.Entities;

namespace AuthSystem.Application.Tests.Fakes;

/// <summary>
/// Hand-written in-memory fake, not a mock - IVaultRepository is small enough that faking it
/// directly avoids pulling in a mocking library for the sake of a handful of methods.
/// </summary>
public class FakeVaultRepository : IVaultRepository
{
    public readonly List<VaultProfile> Profiles = [];
    public readonly List<VaultItem> Items = [];
    public readonly List<VaultKeyShare> Shares = [];

    public Task<VaultProfile?> GetProfileAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult(Profiles.FirstOrDefault(p => p.UserId == userId));

    public Task AddProfileAsync(VaultProfile profile, CancellationToken ct)
    {
        Profiles.Add(profile);
        return Task.CompletedTask;
    }

    public Task<VaultKeyShare?> GetActiveShareAsync(Guid vaultOwnerUserId, Guid holderUserId, CancellationToken ct) =>
        Task.FromResult(Shares.FirstOrDefault(s =>
            s.VaultOwnerUserId == vaultOwnerUserId && s.HolderUserId == holderUserId && s.RevokedAt is null));

    public Task<VaultKeyShare?> GetShareByIdAsync(Guid shareId, CancellationToken ct) =>
        Task.FromResult(Shares.FirstOrDefault(s => s.Id == shareId));

    public Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesGrantedByAsync(Guid vaultOwnerUserId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultKeyShare>>(Shares
            .Where(s => s.VaultOwnerUserId == vaultOwnerUserId && s.HolderUserId != vaultOwnerUserId && s.RevokedAt is null)
            .ToList());

    public Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesHeldByAsync(Guid holderUserId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultKeyShare>>(Shares
            .Where(s => s.HolderUserId == holderUserId && s.VaultOwnerUserId != holderUserId && s.RevokedAt is null)
            .ToList());

    public Task AddShareAsync(VaultKeyShare share, CancellationToken ct)
    {
        Shares.Add(share);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VaultItem>> GetItemsByOwnerAsync(Guid ownerUserId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultItem>>(Items.Where(i => i.OwnerUserId == ownerUserId).ToList());

    public Task<VaultItem?> GetItemAsync(Guid itemId, Guid ownerUserId, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == itemId && i.OwnerUserId == ownerUserId));

    public Task AddItemAsync(VaultItem item, CancellationToken ct)
    {
        Items.Add(item);
        return Task.CompletedTask;
    }

    public void RemoveItem(VaultItem item) => Items.Remove(item);

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}
