using AuthSystem.Application.Dtos;

namespace AuthSystem.Application.Interfaces;

public interface IVaultService
{
    Task<ServiceResult> SetupAsync(Guid userId, VaultSetupRequest request, CancellationToken ct);
    Task<VaultStatusDto> GetStatusAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult<VaultUnlockBundleDto>> GetUnlockBundleAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult> RewrapMasterKeyAsync(Guid userId, VaultRewrapDto request, CancellationToken ct);
    Task<ServiceResult<VaultResetRecoveryBundleDto>> GetResetRecoveryBundleAsync(Guid userId, string resetToken, CancellationToken ct);

    Task<ServiceResult<VaultItemDto>> CreateItemAsync(Guid ownerUserId, CreateVaultItemRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<VaultItemDto>>> GetItemsAsync(Guid ownerUserId, CancellationToken ct);
    Task<ServiceResult<VaultItemDto>> GetItemAsync(Guid ownerUserId, Guid itemId, CancellationToken ct);
    Task<ServiceResult<VaultItemDto>> UpdateItemAsync(Guid ownerUserId, Guid itemId, UpdateVaultItemRequest request, CancellationToken ct);
    Task<ServiceResult> DeleteItemAsync(Guid ownerUserId, Guid itemId, CancellationToken ct);

    Task<ServiceResult<VaultUserLookupDto>> LookupUserAsync(string email, CancellationToken ct);
    Task<ServiceResult<VaultShareDto>> CreateShareAsync(Guid ownerUserId, CreateVaultShareRequest request, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<VaultShareDto>>> GetSharesAsync(Guid ownerUserId, CancellationToken ct);
    Task<ServiceResult> RevokeShareAsync(Guid ownerUserId, Guid shareId, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<VaultSharedWithMeDto>>> GetSharedWithMeAsync(Guid holderUserId, CancellationToken ct);
    Task<ServiceResult<IReadOnlyList<VaultItemDto>>> GetSharedItemsAsync(Guid holderUserId, Guid vaultOwnerUserId, CancellationToken ct);
    Task<ServiceResult> VerifyShareAsync(Guid holderUserId, Guid shareId, string code, CancellationToken ct);
    Task<ServiceResult> ResendShareCodeAsync(Guid holderUserId, Guid shareId, CancellationToken ct);
}
