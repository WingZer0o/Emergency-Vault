using EmergencyVault.Domain.Entities;

namespace EmergencyVault.Application.Interfaces;

/// <summary>
/// EF Core-backed repository for the vault feature's entities - mirrors IRefreshTokenRepository's
/// thin style, since these too aren't managed by UserManager/RoleManager.
/// </summary>
public interface IVaultRepository
{
    Task<VaultProfile?> GetProfileAsync(Guid userId, CancellationToken ct);
    Task AddProfileAsync(VaultProfile profile, CancellationToken ct);

    /// <summary>Pass vaultItemId: null for a whole-vault share, or a specific item's id for an item-scoped share.</summary>
    Task<VaultKeyShare?> GetActiveShareAsync(Guid vaultOwnerUserId, Guid holderUserId, Guid? vaultItemId, CancellationToken ct);
    Task<VaultKeyShare?> GetShareByIdAsync(Guid shareId, CancellationToken ct);
    Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesGrantedByAsync(Guid vaultOwnerUserId, CancellationToken ct);
    Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesHeldByAsync(Guid holderUserId, CancellationToken ct);
    Task<IReadOnlyList<VaultKeyShare>> GetActiveItemSharesHeldByAsync(Guid holderUserId, CancellationToken ct);
    Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesForItemAsync(Guid vaultItemId, CancellationToken ct);
    Task AddShareAsync(VaultKeyShare share, CancellationToken ct);
    void RemoveShare(VaultKeyShare share);
    Task<int> DeleteHoldersSharesByIdAsync(Guid holderUserId, CancellationToken ct);
    Task<int> DeleteItemSharesByItemIdAsync(Guid vaultItemId, CancellationToken ct);
    Task<IReadOnlyList<VaultItem>> GetItemsByOwnerAsync(Guid ownerUserId, CancellationToken ct);
    Task<VaultItem?> GetItemAsync(Guid itemId, Guid ownerUserId, CancellationToken ct);
    Task<VaultItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct);
    Task AddItemAsync(VaultItem item, CancellationToken ct);
    void RemoveItem(VaultItem item);

    Task<VaultItemAttachment?> GetAttachmentAsync(Guid attachmentId, Guid vaultItemId, CancellationToken ct);
    Task<IReadOnlyList<VaultItemAttachment>> GetAttachmentsByItemAsync(Guid vaultItemId, CancellationToken ct);
    Task AddAttachmentAsync(VaultItemAttachment attachment, CancellationToken ct);
    void RemoveAttachment(VaultItemAttachment attachment);

    Task AddItemViewsAsync(IEnumerable<VaultItemView> views, CancellationToken ct);
    Task<IReadOnlyList<VaultItemView>> GetViewsForItemAsync(Guid vaultItemId, CancellationToken ct);
    Task RemoveAllItemRevisionsForItemAsync(Guid changedByUserId, CancellationToken ct);
    Task AddItemRevisionAsync(VaultItemRevision revision, CancellationToken ct);
    Task<IReadOnlyList<VaultItemRevision>> GetRevisionsForItemAsync(Guid vaultItemId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
