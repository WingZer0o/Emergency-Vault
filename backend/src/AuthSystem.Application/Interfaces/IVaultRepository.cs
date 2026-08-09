using AuthSystem.Domain.Entities;

namespace AuthSystem.Application.Interfaces;

/// <summary>
/// EF Core-backed repository for the vault feature's entities - mirrors IRefreshTokenRepository's
/// thin style, since these too aren't managed by UserManager/RoleManager.
/// </summary>
public interface IVaultRepository
{
    Task<VaultProfile?> GetProfileAsync(Guid userId, CancellationToken ct);
    Task AddProfileAsync(VaultProfile profile, CancellationToken ct);

    Task<VaultKeyShare?> GetActiveShareAsync(Guid vaultOwnerUserId, Guid holderUserId, CancellationToken ct);
    Task<VaultKeyShare?> GetShareByIdAsync(Guid shareId, CancellationToken ct);
    Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesGrantedByAsync(Guid vaultOwnerUserId, CancellationToken ct);
    Task<IReadOnlyList<VaultKeyShare>> GetActiveSharesHeldByAsync(Guid holderUserId, CancellationToken ct);
    Task AddShareAsync(VaultKeyShare share, CancellationToken ct);
    void RemoveShare(VaultKeyShare share);
    Task<int> DeleteHoldersSharesByIdAsync(Guid holderUserId, CancellationToken ct);
    Task<IReadOnlyList<VaultItem>> GetItemsByOwnerAsync(Guid ownerUserId, CancellationToken ct);
    Task<VaultItem?> GetItemAsync(Guid itemId, Guid ownerUserId, CancellationToken ct);
    Task AddItemAsync(VaultItem item, CancellationToken ct);
    void RemoveItem(VaultItem item);

    Task SaveChangesAsync(CancellationToken ct);
}
