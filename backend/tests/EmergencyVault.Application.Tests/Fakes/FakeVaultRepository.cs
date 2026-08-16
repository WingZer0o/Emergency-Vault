using EmergencyVault.Application.Interfaces;
using EmergencyVault.Domain.Entities;

namespace EmergencyVault.Application.Tests.Fakes;

/// <summary>
/// Hand-written in-memory fake, not a mock - IVaultRepository is small enough that faking it
/// directly avoids pulling in a mocking library for the sake of a handful of methods.
/// </summary>
public class FakeVaultRepository : IVaultRepository
{
    public readonly List<VaultProfile> Profiles = [];
    public readonly List<VaultItem> Items = [];
    public readonly List<VaultKeyShare> Shares = [];
    public readonly List<VaultItemAttachment> Attachments = [];
    public readonly List<VaultItemView> ItemViews = [];
    public readonly List<VaultItemRevision> ItemRevisions = [];

    public Task<VaultProfile?> GetProfileAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult(Profiles.FirstOrDefault(p => p.UserId == userId));

    public Task AddProfileAsync(VaultProfile profile, CancellationToken ct)
    {
        Profiles.Add(profile);
        return Task.CompletedTask;
    }

    public Task<VaultKeyShare?> GetActiveShareAsync(Guid vaultOwnerUserId, Guid holderUserId, Guid? vaultItemId, CancellationToken ct) =>
        Task.FromResult(Shares.FirstOrDefault(s =>
            s.VaultOwnerUserId == vaultOwnerUserId && s.HolderUserId == holderUserId
                && s.VaultItemId == vaultItemId && s.RevokedAt is null));

    public Task<VaultKeyShare?> GetShareByIdAsync(Guid shareId, CancellationToken ct) =>
        Task.FromResult(Shares.FirstOrDefault(s => s.Id == shareId));

    public Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesGrantedByAsync(Guid vaultOwnerUserId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultKeyShare>>(Shares
            .Where(s => s.VaultOwnerUserId == vaultOwnerUserId && s.HolderUserId != vaultOwnerUserId
                && s.VaultItemId == null && s.RevokedAt is null)
            .ToList());

    public Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesHeldByAsync(Guid holderUserId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultKeyShare>>(Shares
            .Where(s => s.HolderUserId == holderUserId && s.VaultOwnerUserId != holderUserId
                && s.VaultItemId == null && s.RevokedAt is null)
            .ToList());

    public Task<IReadOnlyList<VaultKeyShare>> GetActiveItemSharesHeldByAsync(Guid holderUserId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultKeyShare>>(Shares
            .Where(s => s.HolderUserId == holderUserId && s.VaultItemId != null && s.RevokedAt is null)
            .ToList());

    public Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesForItemAsync(Guid vaultItemId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultKeyShare>>(Shares
            .Where(s => s.VaultItemId == vaultItemId && s.RevokedAt is null)
            .ToList());

    public Task<IReadOnlyList<VaultKeyShare>> GetSharesHeldByAsync(Guid holderUserId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultKeyShare>>(Shares.Where(s => s.HolderUserId == holderUserId).ToList());

    public Task AddShareAsync(VaultKeyShare share, CancellationToken ct)
    {
        Shares.Add(share);
        return Task.CompletedTask;
    }

    public void RemoveShare(VaultKeyShare share) => Shares.Remove(share);

    public Task<int> DeleteHoldersSharesByIdAsync(Guid holderUserId, CancellationToken ct)
    {
        var removed = Shares.RemoveAll(s => s.HolderUserId == holderUserId);
        return Task.FromResult(removed);
    }

    public Task<int> DeleteItemSharesByItemIdAsync(Guid vaultItemId, CancellationToken ct)
    {
        var removed = Shares.RemoveAll(s => s.VaultItemId == vaultItemId);
        return Task.FromResult(removed);
    }

    public Task<IReadOnlyList<VaultItem>> GetItemsByOwnerAsync(Guid ownerUserId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultItem>>(Items.Where(i => i.OwnerUserId == ownerUserId).ToList());

    public Task<VaultItem?> GetItemAsync(Guid itemId, Guid ownerUserId, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == itemId && i.OwnerUserId == ownerUserId));

    public Task<VaultItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == itemId));

    public Task AddItemAsync(VaultItem item, CancellationToken ct)
    {
        Items.Add(item);
        return Task.CompletedTask;
    }

    public void RemoveItem(VaultItem item) => Items.Remove(item);

    public Task<VaultItemAttachment?> GetAttachmentAsync(Guid attachmentId, Guid vaultItemId, CancellationToken ct) =>
        Task.FromResult(Attachments.FirstOrDefault(a => a.Id == attachmentId && a.VaultItemId == vaultItemId));

    public Task<IReadOnlyList<VaultItemAttachment>> GetAttachmentsByItemAsync(Guid vaultItemId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultItemAttachment>>(Attachments.Where(a => a.VaultItemId == vaultItemId).ToList());

    public Task AddAttachmentAsync(VaultItemAttachment attachment, CancellationToken ct)
    {
        Attachments.Add(attachment);
        return Task.CompletedTask;
    }

    public void RemoveAttachment(VaultItemAttachment attachment) => Attachments.Remove(attachment);

    public Task AddItemViewsAsync(IEnumerable<VaultItemView> views, CancellationToken ct)
    {
        ItemViews.AddRange(views);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VaultItemView>> GetViewsForItemAsync(Guid vaultItemId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultItemView>>(
            ItemViews.Where(v => v.VaultItemId == vaultItemId).OrderByDescending(v => v.ViewedAt).Take(50).ToList());

    public Task RemoveAllItemRevisionsForItemAsync(Guid changedByUserId, CancellationToken ct)
    {
        ItemRevisions.RemoveAll(r => r.ChangedByUserId == changedByUserId);
        return Task.CompletedTask;
    }

    public Task AddItemRevisionAsync(VaultItemRevision revision, CancellationToken ct)
    {
        ItemRevisions.Add(revision);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VaultItemRevision>> GetRevisionsForItemAsync(Guid vaultItemId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<VaultItemRevision>>(
            ItemRevisions.Where(r => r.VaultItemId == vaultItemId).OrderByDescending(r => r.ChangedAt).Take(50).ToList());

    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
}
