using AuthSystem.Application.Dtos;
using AuthSystem.Application.Interfaces;
using AuthSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthSystem.Application.Services;

public class VaultService(IVaultRepository vaultRepository, UserManager<ApplicationUser> userManager) : IVaultService
{
    public async Task<ServiceResult> SetupAsync(Guid userId, VaultSetupRequest request, CancellationToken ct)
    {
        var existing = await vaultRepository.GetProfileAsync(userId, ct);
        if (existing is not null)
        {
            return ServiceResult.Failure("Vault is already set up.");
        }

        var profile = new VaultProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PublicKey = request.PublicKey,
            KdfSalt = request.KdfSalt,
            KdfIterations = request.KdfIterations,
            PrivateKeyWrappedByMasterKey = request.PrivateKeyWrappedByMasterKey,
            PrivateKeyWrappedByRecoveryKey = request.PrivateKeyWrappedByRecoveryKey,
        };
        await vaultRepository.AddProfileAsync(profile, ct);

        // Self-share: the owner's own access uses the exact same wrap/unwrap mechanism as an
        // emergency contact's, so "unlock my vault" and "open a vault shared with me" are the
        // same code path everywhere else in this service.
        await vaultRepository.AddShareAsync(new VaultKeyShare
        {
            Id = Guid.NewGuid(),
            VaultOwnerUserId = userId,
            HolderUserId = userId,
            WrappedUvk = request.SelfWrappedUvk,
        }, ct);

        try
        {
            await vaultRepository.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Unique index on VaultProfile.UserId - handles a concurrent double-setup race.
            return ServiceResult.Failure("Vault is already set up.");
        }

        return ServiceResult.Success();
    }

    public async Task<VaultStatusDto> GetStatusAsync(Guid userId, CancellationToken ct)
    {
        var profile = await vaultRepository.GetProfileAsync(userId, ct);
        return new VaultStatusDto(profile is not null);
    }

    public async Task<ServiceResult<VaultUnlockBundleDto>> GetUnlockBundleAsync(Guid userId, CancellationToken ct)
    {
        var profile = await vaultRepository.GetProfileAsync(userId, ct);
        var selfShare = profile == null ? null : await vaultRepository.GetActiveShareAsync(userId, userId, ct);
        if (profile is null || selfShare is null)
        {
            return ServiceResult<VaultUnlockBundleDto>.Failure("Vault has not been set up.");
        }

        return ServiceResult<VaultUnlockBundleDto>.Success(new VaultUnlockBundleDto(
            profile.KdfSalt,
            profile.KdfIterations,
            profile.PrivateKeyWrappedByMasterKey,
            profile.PrivateKeyWrappedByRecoveryKey,
            selfShare.WrappedUvk));
    }

    public async Task<ServiceResult> RewrapMasterKeyAsync(Guid userId, VaultRewrapDto request, CancellationToken ct)
    {
        var profile = await vaultRepository.GetProfileAsync(userId, ct);
        if (profile is null)
        {
            return ServiceResult.Failure("Vault has not been set up.");
        }

        profile.KdfSalt = request.NewKdfSalt;
        profile.KdfIterations = request.NewKdfIterations;
        profile.PrivateKeyWrappedByMasterKey = request.NewPrivateKeyWrappedByMasterKey;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await vaultRepository.SaveChangesAsync(ct);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<VaultResetRecoveryBundleDto>> GetResetRecoveryBundleAsync(Guid userId, string resetToken, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return ServiceResult<VaultResetRecoveryBundleDto>.Failure("Invalid or expired reset link.");
        }

        // Read-only check against the same token a normal /auth/reset-password call would consume -
        // does not invalidate it, so the user can still complete the actual reset afterward.
        var isValid = await userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "ResetPassword", resetToken);
        if (!isValid)
        {
            return ServiceResult<VaultResetRecoveryBundleDto>.Failure("Invalid or expired reset link.");
        }

        var profile = await vaultRepository.GetProfileAsync(userId, ct);
        if (profile is null)
        {
            return ServiceResult<VaultResetRecoveryBundleDto>.Failure("No vault set up for this account.");
        }

        return ServiceResult<VaultResetRecoveryBundleDto>.Success(new VaultResetRecoveryBundleDto(profile.PrivateKeyWrappedByRecoveryKey));
    }

    public async Task<ServiceResult<VaultItemDto>> CreateItemAsync(Guid ownerUserId, CreateVaultItemRequest request, CancellationToken ct)
    {
        var profile = await vaultRepository.GetProfileAsync(ownerUserId, ct);
        if (profile is null)
        {
            return ServiceResult<VaultItemDto>.Failure("Vault has not been set up.");
        }

        var item = new VaultItem
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            CiphertextBlob = request.CiphertextBlob,
        };
        await vaultRepository.AddItemAsync(item, ct);
        await vaultRepository.SaveChangesAsync(ct);

        return ServiceResult<VaultItemDto>.Success(ToDto(item));
    }

    public async Task<ServiceResult<IReadOnlyList<VaultItemDto>>> GetItemsAsync(Guid ownerUserId, CancellationToken ct)
    {
        var items = await vaultRepository.GetItemsByOwnerAsync(ownerUserId, ct);
        return ServiceResult<IReadOnlyList<VaultItemDto>>.Success(items.Select(ToDto).ToList());
    }

    public async Task<ServiceResult<VaultItemDto>> GetItemAsync(Guid ownerUserId, Guid itemId, CancellationToken ct)
    {
        var item = await vaultRepository.GetItemAsync(itemId, ownerUserId, ct);
        return item is null
            ? ServiceResult<VaultItemDto>.Failure("Item not found.")
            : ServiceResult<VaultItemDto>.Success(ToDto(item));
    }

    public async Task<ServiceResult<VaultItemDto>> UpdateItemAsync(Guid ownerUserId, Guid itemId, UpdateVaultItemRequest request, CancellationToken ct)
    {
        var item = await vaultRepository.GetItemAsync(itemId, ownerUserId, ct);
        if (item is null)
        {
            return ServiceResult<VaultItemDto>.Failure("Item not found.");
        }

        item.CiphertextBlob = request.CiphertextBlob;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await vaultRepository.SaveChangesAsync(ct);

        return ServiceResult<VaultItemDto>.Success(ToDto(item));
    }

    public async Task<ServiceResult> DeleteItemAsync(Guid ownerUserId, Guid itemId, CancellationToken ct)
    {
        var item = await vaultRepository.GetItemAsync(itemId, ownerUserId, ct);
        if (item is null)
        {
            return ServiceResult.Failure("Item not found.");
        }

        vaultRepository.RemoveItem(item);
        await vaultRepository.SaveChangesAsync(ct);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<VaultUserLookupDto>> LookupUserAsync(string email, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(email);
        var profile = user is null ? null : await vaultRepository.GetProfileAsync(user.Id, ct);
        if (user is null || profile is null)
        {
            // Same generic failure whether the email doesn't exist or just has no vault yet -
            // avoids letting this endpoint be used to enumerate registered emails.
            return ServiceResult<VaultUserLookupDto>.Failure("No user found with that email, or they haven't set up their vault yet.");
        }

        return ServiceResult<VaultUserLookupDto>.Success(new VaultUserLookupDto(user.Id, user.FirstName, user.LastName, profile.PublicKey));
    }

    public async Task<ServiceResult<VaultShareDto>> CreateShareAsync(Guid ownerUserId, CreateVaultShareRequest request, CancellationToken ct)
    {
        var ownerProfile = await vaultRepository.GetProfileAsync(ownerUserId, ct);
        if (ownerProfile is null)
        {
            return ServiceResult<VaultShareDto>.Failure("Set up your own vault before sharing it.");
        }

        var holder = await userManager.FindByEmailAsync(request.HolderEmail);
        if (holder is null || holder.Id == ownerUserId)
        {
            return ServiceResult<VaultShareDto>.Failure(
                holder is null
                    ? "No user found with that email, or they haven't set up their vault yet."
                    : "You can't grant access to yourself.");
        }

        var holderProfile = await vaultRepository.GetProfileAsync(holder.Id, ct);
        if (holderProfile is null)
        {
            return ServiceResult<VaultShareDto>.Failure("No user found with that email, or they haven't set up their vault yet.");
        }

        var existing = await vaultRepository.GetActiveShareAsync(ownerUserId, holder.Id, ct);
        if (existing is { } existingShare)
        {
            existingShare.RevokedAt = DateTimeOffset.UtcNow;
        }

        var share = new VaultKeyShare
        {
            Id = Guid.NewGuid(),
            VaultOwnerUserId = ownerUserId,
            HolderUserId = holder.Id,
            WrappedUvk = request.WrappedUvk,
        };
        await vaultRepository.AddShareAsync(share, ct);
        await vaultRepository.SaveChangesAsync(ct);

        return ServiceResult<VaultShareDto>.Success(
            new VaultShareDto(share.Id, holder.Id, $"{holder.FirstName} {holder.LastName}", holder.Email!, share.GrantedAt));
    }

    public async Task<ServiceResult<IReadOnlyList<VaultShareDto>>> GetSharesAsync(Guid ownerUserId, CancellationToken ct)
    {
        var shares = await vaultRepository.GetActiveSharesGrantedByAsync(ownerUserId, ct);
        var dtos = shares
            .Select(s => new VaultShareDto(s.Id, s.HolderUserId, $"{s.Holder.FirstName} {s.Holder.LastName}", s.Holder.Email!, s.GrantedAt))
            .ToList();
        return ServiceResult<IReadOnlyList<VaultShareDto>>.Success(dtos);
    }

    public async Task<ServiceResult> RevokeShareAsync(Guid ownerUserId, Guid shareId, CancellationToken ct)
    {
        var share = await vaultRepository.GetShareByIdAsync(shareId, ct);
        if (share is null || share.VaultOwnerUserId != ownerUserId || share.RevokedAt is not null)
        {
            return ServiceResult.Failure("Share not found.");
        }

        if (share.HolderUserId == ownerUserId)
        {
            // Defense in depth - the self-share row is never surfaced via GetSharesAsync, so this
            // shouldn't be reachable through the UI, but never allow revoking your own access.
            return ServiceResult.Failure("Cannot revoke your own vault access.");
        }

        share.RevokedAt = DateTimeOffset.UtcNow;
        await vaultRepository.SaveChangesAsync(ct);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult<IReadOnlyList<VaultSharedWithMeDto>>> GetSharedWithMeAsync(Guid holderUserId, CancellationToken ct)
    {
        var shares = await vaultRepository.GetActiveSharesHeldByAsync(holderUserId, ct);
        var dtos = shares
            .Select(s => new VaultSharedWithMeDto(s.VaultOwnerUserId, $"{s.VaultOwner.FirstName} {s.VaultOwner.LastName}", s.VaultOwner.Email!, s.WrappedUvk, s.GrantedAt))
            .ToList();
        return ServiceResult<IReadOnlyList<VaultSharedWithMeDto>>.Success(dtos);
    }

    public async Task<ServiceResult<IReadOnlyList<VaultItemDto>>> GetSharedItemsAsync(Guid holderUserId, Guid vaultOwnerUserId, CancellationToken ct)
    {
        var share = await vaultRepository.GetActiveShareAsync(vaultOwnerUserId, holderUserId, ct);
        if (share is null)
        {
            return ServiceResult<IReadOnlyList<VaultItemDto>>.Failure("You don't have access to this vault.");
        }

        var items = await vaultRepository.GetItemsByOwnerAsync(vaultOwnerUserId, ct);
        return ServiceResult<IReadOnlyList<VaultItemDto>>.Success(items.Select(ToDto).ToList());
    }

    private static VaultItemDto ToDto(VaultItem item) => new(item.Id, item.CiphertextBlob, item.CreatedAt, item.UpdatedAt);
}
