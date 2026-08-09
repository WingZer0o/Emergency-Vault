using System.Security.Cryptography;
using System.Text;
using AuthSystem.Application.Dtos;
using AuthSystem.Application.Interfaces;
using AuthSystem.Application.Options;
using AuthSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthSystem.Application.Services;

public class VaultService(
    IVaultRepository vaultRepository,
    IVaultAttachmentBlobStore attachmentBlobStore,
    UserManager<ApplicationUser> userManager,
    IHotpService hotpService,
    IEmailSender emailSender,
    ISharedVaultAccessChallengeCache sharedVaultAccessChallengeCache,
    ISharedVaultAccessGrantCache sharedVaultAccessGrantCache,
    IOptions<VaultShareCodesOptions> vaultShareCodesOptions) : IVaultService
{
    private readonly VaultShareCodesOptions _vaultShareCodesOptions = vaultShareCodesOptions.Value;

    private static readonly HashSet<string> AllowedAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx" };
    private static readonly HashSet<string> AllowedAttachmentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    // varbinary(max)'s 2^31-1 byte ceiling, minus the 16-byte AES-GCM auth tag appended to the ciphertext.
    private const long MaxAttachmentSizeBytes = 2_147_483_631;

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
            KeySchemeVersion = 2,
            MlKemPublicKey = request.MlKemPublicKey,
            MlKemSecretKeyWrappedByMasterKey = request.MlKemSecretKeyWrappedByMasterKey,
            MlKemSecretKeyWrappedByRecoveryKey = request.MlKemSecretKeyWrappedByRecoveryKey,
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
            // Self-share never goes through the emailed-code flow - it's usable immediately.
            VerifiedAt = DateTimeOffset.UtcNow,
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
            selfShare.WrappedUvk,
            profile.MlKemPublicKey,
            profile.MlKemSecretKeyWrappedByMasterKey,
            profile.MlKemSecretKeyWrappedByRecoveryKey));
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

        // A legacy (KeySchemeVersion 1) account's reset/recover flow never sends this - only
        // overwrite when a value is actually provided, so we never null out a hybrid account's
        // stored ML-KEM secret key wrapping.
        if (request.NewMlKemSecretKeyWrappedByMasterKey is not null)
        {
            profile.MlKemSecretKeyWrappedByMasterKey = request.NewMlKemSecretKeyWrappedByMasterKey;
        }

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

        return ServiceResult<VaultResetRecoveryBundleDto>.Success(
            new VaultResetRecoveryBundleDto(profile.PrivateKeyWrappedByRecoveryKey, profile.MlKemSecretKeyWrappedByRecoveryKey));
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

    public async Task<ServiceResult<VaultItemAttachmentDto>> UploadAttachmentAsync(
        Guid callerId, Guid itemId, string fileName, string contentType, string ivBase64, long sizeBytes, Stream content, CancellationToken ct)
    {
        // Write access is owner-only, matching UpdateItemAsync/DeleteItemAsync - there is no
        // shared-holder write path anywhere in the vault today.
        var item = await vaultRepository.GetItemAsync(itemId, callerId, ct);
        if (item is null)
        {
            return ServiceResult<VaultItemAttachmentDto>.Failure("Item not found.");
        }

        if (string.IsNullOrWhiteSpace(fileName) || !AllowedAttachmentExtensions.Contains(Path.GetExtension(fileName)))
        {
            return ServiceResult<VaultItemAttachmentDto>.Failure("Only .pdf and .docx files are allowed.");
        }

        if (string.IsNullOrWhiteSpace(contentType) || !AllowedAttachmentContentTypes.Contains(contentType))
        {
            return ServiceResult<VaultItemAttachmentDto>.Failure("Only .pdf and .docx files are allowed.");
        }

        if (sizeBytes <= 0 || sizeBytes > MaxAttachmentSizeBytes)
        {
            return ServiceResult<VaultItemAttachmentDto>.Failure("File is too large.");
        }

        if (string.IsNullOrWhiteSpace(ivBase64))
        {
            return ServiceResult<VaultItemAttachmentDto>.Failure("Missing encryption IV.");
        }

        var attachment = new VaultItemAttachment
        {
            Id = Guid.NewGuid(),
            VaultItemId = item.Id,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Iv = ivBase64,
        };

        // Metadata row first (via EF, so it gets a generated Id others can reference), then the
        // ciphertext bytes are streamed in separately via raw ADO.NET - see IVaultAttachmentBlobStore.
        await vaultRepository.AddAttachmentAsync(attachment, ct);
        await vaultRepository.SaveChangesAsync(ct);
        await attachmentBlobStore.WriteAsync(attachment.Id, content, ct);

        return ServiceResult<VaultItemAttachmentDto>.Success(ToAttachmentDto(attachment));
    }

    public async Task<ServiceResult<IReadOnlyList<VaultItemAttachmentDto>>> GetAttachmentsAsync(Guid callerId, Guid itemId, CancellationToken ct)
    {
        var item = await vaultRepository.GetItemByIdAsync(itemId, ct);
        if (item is null || !await CanAccessItemAsync(callerId, item.OwnerUserId, ct))
        {
            return ServiceResult<IReadOnlyList<VaultItemAttachmentDto>>.Failure("Item not found.");
        }

        var attachments = await vaultRepository.GetAttachmentsByItemAsync(item.Id, ct);
        return ServiceResult<IReadOnlyList<VaultItemAttachmentDto>>.Success(attachments.Select(ToAttachmentDto).ToList());
    }

    public async Task<ServiceResult<(VaultItemAttachmentDto Meta, Stream Content)>> DownloadAttachmentAsync(
        Guid callerId, Guid itemId, Guid attachmentId, CancellationToken ct)
    {
        var item = await vaultRepository.GetItemByIdAsync(itemId, ct);
        if (item is null || !await CanAccessItemAsync(callerId, item.OwnerUserId, ct))
        {
            return ServiceResult<(VaultItemAttachmentDto Meta, Stream Content)>.Failure("Item not found.");
        }

        var attachment = await vaultRepository.GetAttachmentAsync(attachmentId, item.Id, ct);
        if (attachment is null)
        {
            return ServiceResult<(VaultItemAttachmentDto Meta, Stream Content)>.Failure("Attachment not found.");
        }

        var stream = await attachmentBlobStore.OpenReadAsync(attachment.Id, ct);
        return ServiceResult<(VaultItemAttachmentDto Meta, Stream Content)>.Success((ToAttachmentDto(attachment), stream));
    }

    public async Task<ServiceResult> DeleteAttachmentAsync(Guid callerId, Guid itemId, Guid attachmentId, CancellationToken ct)
    {
        // Write access is owner-only - same rationale as UploadAttachmentAsync.
        var item = await vaultRepository.GetItemAsync(itemId, callerId, ct);
        if (item is null)
        {
            return ServiceResult.Failure("Item not found.");
        }

        var attachment = await vaultRepository.GetAttachmentAsync(attachmentId, item.Id, ct);
        if (attachment is null)
        {
            return ServiceResult.Failure("Attachment not found.");
        }

        vaultRepository.RemoveAttachment(attachment);
        await vaultRepository.SaveChangesAsync(ct);
        return ServiceResult.Success();
    }

    /// <summary>Owner-or-verified-share read-access check, mirroring the inline logic in GetSharedItemsAsync.</summary>
    private async Task<bool> CanAccessItemAsync(Guid callerId, Guid ownerUserId, CancellationToken ct)
    {
        if (callerId == ownerUserId)
        {
            return true;
        }

        var share = await vaultRepository.GetActiveShareAsync(ownerUserId, callerId, ct);
        if (share is null || share.VerifiedAt is null)
        {
            return false;
        }

        return await sharedVaultAccessGrantCache.HasValidGrantAsync(callerId, ownerUserId, ct);
    }

    private static VaultItemAttachmentDto ToAttachmentDto(VaultItemAttachment a) =>
        new(a.Id, a.VaultItemId, a.FileName, a.ContentType, a.SizeBytes, a.Iv, a.CreatedAt);

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

        return ServiceResult<VaultUserLookupDto>.Success(
            new VaultUserLookupDto(user.Id, user.FirstName, user.LastName, profile.PublicKey, profile.MlKemPublicKey));
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

        var owner = await userManager.FindByIdAsync(ownerUserId.ToString())
            ?? throw new InvalidOperationException("Vault owner not found.");

        var now = DateTimeOffset.UtcNow;
        var share = new VaultKeyShare
        {
            Id = Guid.NewGuid(),
            VaultOwnerUserId = ownerUserId,
            VaultOwner = owner,
            HolderUserId = holder.Id,
            Holder = holder,
            WrappedUvk = request.WrappedUvk,
            VerificationSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(20)),
            VerificationCounter = 1,
            VerificationCodeExpiresAt = now + _vaultShareCodesOptions.CodeExpiration,
            VerificationCodeSentAt = now,
        };
        await vaultRepository.AddShareAsync(share, ct);
        await vaultRepository.SaveChangesAsync(ct);

        var code = hotpService.GenerateCode(
            Convert.FromBase64String(share.VerificationSecret), share.VerificationCounter, _vaultShareCodesOptions.CodeLength);
        await emailSender.SendVaultShareCodeAsync(holder.Email!, code, $"{owner.FirstName} {owner.LastName}", ct);

        return ServiceResult<VaultShareDto>.Success(
            new VaultShareDto(share.Id, holder.Id, $"{holder.FirstName} {holder.LastName}", holder.Email!, share.GrantedAt, ComputeStatus(share)));
    }

    public async Task<ServiceResult<IReadOnlyList<VaultShareDto>>> GetSharesAsync(Guid ownerUserId, CancellationToken ct)
    {
        var shares = await vaultRepository.GetActiveSharesGrantedByAsync(ownerUserId, ct);
        var dtos = shares
            .Select(s => new VaultShareDto(s.Id, s.HolderUserId, $"{s.Holder.FirstName} {s.Holder.LastName}", s.Holder.Email!, s.GrantedAt, ComputeStatus(s)))
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
            .Select(s => new VaultSharedWithMeDto(
                s.Id, s.VaultOwnerUserId, $"{s.VaultOwner.FirstName} {s.VaultOwner.LastName}", s.VaultOwner.Email!,
                s.VerifiedAt is not null ? s.WrappedUvk : null, s.GrantedAt, ComputeStatus(s)))
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

        if (share.VerifiedAt is null)
        {
            return ServiceResult<IReadOnlyList<VaultItemDto>>.Failure("This shared vault hasn't been verified yet.");
        }

        if (holderUserId != vaultOwnerUserId && !await sharedVaultAccessGrantCache.HasValidGrantAsync(holderUserId, vaultOwnerUserId, ct))
        {
            return ServiceResult<IReadOnlyList<VaultItemDto>>.Failure("Enter the emailed code to view this vault.");
        }

        var items = await vaultRepository.GetItemsByOwnerAsync(vaultOwnerUserId, ct);
        return ServiceResult<IReadOnlyList<VaultItemDto>>.Success(items.Select(ToDto).ToList());
    }

    public async Task<ServiceResult> VerifyShareAsync(Guid holderUserId, Guid shareId, string code, CancellationToken ct)
    {
        var share = await vaultRepository.GetShareByIdAsync(shareId, ct);
        if (share is null || share.HolderUserId != holderUserId || share.RevokedAt is not null)
        {
            return ServiceResult.Failure("Share not found.");
        }

        if (share.VerifiedAt is not null)
        {
            return ServiceResult.Success();
        }

        if (share.VerificationAttempts >= _vaultShareCodesOptions.MaxAttempts)
        {
            return ServiceResult.Failure("Too many incorrect attempts. Ask the owner to share the vault again.");
        }

        if (share.VerificationCodeExpiresAt is null || DateTimeOffset.UtcNow > share.VerificationCodeExpiresAt)
        {
            return ServiceResult.Failure("This code has expired. Request a new one.");
        }

        var expectedCode = hotpService.GenerateCode(
            Convert.FromBase64String(share.VerificationSecret!), share.VerificationCounter, _vaultShareCodesOptions.CodeLength);

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(code), Encoding.UTF8.GetBytes(expectedCode)))
        {
            share.VerificationAttempts += 1;
            await vaultRepository.SaveChangesAsync(ct);

            return share.VerificationAttempts >= _vaultShareCodesOptions.MaxAttempts
                ? ServiceResult.Failure("Too many incorrect attempts. Ask the owner to share the vault again.")
                : ServiceResult.Failure("Incorrect code.");
        }

        share.VerifiedAt = DateTimeOffset.UtcNow;
        await vaultRepository.SaveChangesAsync(ct);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ResendShareCodeAsync(Guid holderUserId, Guid shareId, CancellationToken ct)
    {
        var share = await vaultRepository.GetShareByIdAsync(shareId, ct);
        if (share is null || share.HolderUserId != holderUserId || share.RevokedAt is not null)
        {
            return ServiceResult.Failure("Share not found.");
        }

        if (share.VerifiedAt is not null)
        {
            return ServiceResult.Failure("This share has already been verified.");
        }

        if (share.VerificationAttempts >= _vaultShareCodesOptions.MaxAttempts)
        {
            return ServiceResult.Failure("Too many incorrect attempts. Ask the owner to share the vault again.");
        }

        var now = DateTimeOffset.UtcNow;
        if (share.VerificationCodeSentAt is not null && now - share.VerificationCodeSentAt < _vaultShareCodesOptions.ResendCooldown)
        {
            return ServiceResult.Failure("Please wait a bit before requesting another code.");
        }

        var owner = await userManager.FindByIdAsync(share.VaultOwnerUserId.ToString())
            ?? throw new InvalidOperationException("Vault owner not found.");
        var holder = await userManager.FindByIdAsync(holderUserId.ToString())
            ?? throw new InvalidOperationException("Holder not found.");

        share.VerificationCounter += 1;
        share.VerificationAttempts = 0;
        share.VerificationCodeExpiresAt = now + _vaultShareCodesOptions.CodeExpiration;
        share.VerificationCodeSentAt = now;
        await vaultRepository.SaveChangesAsync(ct);

        var code = hotpService.GenerateCode(
            Convert.FromBase64String(share.VerificationSecret!), share.VerificationCounter, _vaultShareCodesOptions.CodeLength);
        await emailSender.SendVaultShareCodeAsync(holder.Email!, code, $"{owner.FirstName} {owner.LastName}", ct);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<SharedVaultAccessChallengeDto>> RequestSharedVaultAccessAsync(Guid holderUserId, Guid ownerUserId, CancellationToken ct)
    {
        if (holderUserId == ownerUserId)
        {
            // Self-share: the owner viewing their own vault never needs a code.
            return ServiceResult<SharedVaultAccessChallengeDto>.Success(new SharedVaultAccessChallengeDto(false, null));
        }

        var share = await vaultRepository.GetActiveShareAsync(ownerUserId, holderUserId, ct);
        if (share is null || share.VerifiedAt is null)
        {
            return ServiceResult<SharedVaultAccessChallengeDto>.Failure("You don't have access to this vault.");
        }

        if (await sharedVaultAccessGrantCache.HasValidGrantAsync(holderUserId, ownerUserId, ct))
        {
            return ServiceResult<SharedVaultAccessChallengeDto>.Success(new SharedVaultAccessChallengeDto(false, null));
        }

        // Shares accepted before per-session access codes existed never got a long-lived secret -
        // generate one lazily the first time it's needed.
        share.VerificationSecret ??= Convert.ToBase64String(RandomNumberGenerator.GetBytes(20));

        var owner = await userManager.FindByIdAsync(ownerUserId.ToString())
            ?? throw new InvalidOperationException("Vault owner not found.");
        var holder = await userManager.FindByIdAsync(holderUserId.ToString())
            ?? throw new InvalidOperationException("Holder not found.");

        share.VerificationCounter += 1;
        await vaultRepository.SaveChangesAsync(ct);

        var code = hotpService.GenerateCode(
            Convert.FromBase64String(share.VerificationSecret), share.VerificationCounter, _vaultShareCodesOptions.CodeLength);
        await emailSender.SendSharedVaultAccessCodeAsync(holder.Email!, code, $"{owner.FirstName} {owner.LastName}", ct);

        var challengeToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await sharedVaultAccessChallengeCache.SetAsync(
            challengeToken,
            new SharedVaultAccessChallenge(holderUserId, ownerUserId, share.Id, share.VerificationCounter, 0, DateTimeOffset.UtcNow),
            _vaultShareCodesOptions.AccessCodeExpiration,
            ct);

        return ServiceResult<SharedVaultAccessChallengeDto>.Success(new SharedVaultAccessChallengeDto(true, challengeToken));
    }

    public async Task<ServiceResult> VerifySharedVaultAccessAsync(Guid holderUserId, string challengeToken, string code, CancellationToken ct)
    {
        var challenge = await sharedVaultAccessChallengeCache.GetAsync(challengeToken, ct);
        if (challenge is null || challenge.HolderUserId != holderUserId)
        {
            return ServiceResult.Failure("Invalid or expired code. Request a new one.");
        }

        if (challenge.Attempts >= _vaultShareCodesOptions.MaxAttempts)
        {
            await sharedVaultAccessChallengeCache.RemoveAsync(challengeToken, ct);
            return ServiceResult.Failure("Too many incorrect attempts. Request a new code.");
        }

        var share = await vaultRepository.GetShareByIdAsync(challenge.ShareId, ct);
        if (share is null || share.RevokedAt is not null || share.VerificationSecret is null)
        {
            await sharedVaultAccessChallengeCache.RemoveAsync(challengeToken, ct);
            return ServiceResult.Failure("Invalid or expired code. Request a new one.");
        }

        var expectedCode = hotpService.GenerateCode(
            Convert.FromBase64String(share.VerificationSecret), challenge.Counter, _vaultShareCodesOptions.CodeLength);

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(code), Encoding.UTF8.GetBytes(expectedCode)))
        {
            var attempts = challenge.Attempts + 1;
            if (attempts >= _vaultShareCodesOptions.MaxAttempts)
            {
                await sharedVaultAccessChallengeCache.RemoveAsync(challengeToken, ct);
                return ServiceResult.Failure("Too many incorrect attempts. Request a new code.");
            }

            await sharedVaultAccessChallengeCache.SetAsync(
                challengeToken, challenge with { Attempts = attempts }, _vaultShareCodesOptions.AccessCodeExpiration, ct);
            return ServiceResult.Failure("Incorrect code.");
        }

        await sharedVaultAccessGrantCache.GrantAsync(holderUserId, challenge.OwnerUserId, _vaultShareCodesOptions.SessionTtl, ct);
        await sharedVaultAccessChallengeCache.RemoveAsync(challengeToken, ct);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ResendSharedVaultAccessCodeAsync(Guid holderUserId, string challengeToken, CancellationToken ct)
    {
        var challenge = await sharedVaultAccessChallengeCache.GetAsync(challengeToken, ct);
        if (challenge is null || challenge.HolderUserId != holderUserId)
        {
            return ServiceResult.Failure("Invalid or expired code. Request a new one.");
        }

        var now = DateTimeOffset.UtcNow;
        if (now - challenge.SentAt < _vaultShareCodesOptions.ResendCooldown)
        {
            return ServiceResult.Failure("Please wait a bit before requesting another code.");
        }

        var share = await vaultRepository.GetShareByIdAsync(challenge.ShareId, ct);
        if (share is null || share.RevokedAt is not null || share.VerificationSecret is null)
        {
            await sharedVaultAccessChallengeCache.RemoveAsync(challengeToken, ct);
            return ServiceResult.Failure("Invalid or expired code. Request a new one.");
        }

        var owner = await userManager.FindByIdAsync(challenge.OwnerUserId.ToString())
            ?? throw new InvalidOperationException("Vault owner not found.");
        var holder = await userManager.FindByIdAsync(holderUserId.ToString())
            ?? throw new InvalidOperationException("Holder not found.");

        share.VerificationCounter += 1;
        await vaultRepository.SaveChangesAsync(ct);

        var code = hotpService.GenerateCode(
            Convert.FromBase64String(share.VerificationSecret), share.VerificationCounter, _vaultShareCodesOptions.CodeLength);
        await emailSender.SendSharedVaultAccessCodeAsync(holder.Email!, code, $"{owner.FirstName} {owner.LastName}", ct);

        await sharedVaultAccessChallengeCache.SetAsync(
            challengeToken,
            challenge with { Counter = share.VerificationCounter, Attempts = 0, SentAt = now },
            _vaultShareCodesOptions.AccessCodeExpiration,
            ct);

        return ServiceResult.Success();
    }

    private VaultShareStatus ComputeStatus(VaultKeyShare share)
    {
        if (share.VerifiedAt is not null)
        {
            return VaultShareStatus.Active;
        }

        if (share.VerificationAttempts >= _vaultShareCodesOptions.MaxAttempts)
        {
            return VaultShareStatus.Failed;
        }

        if (share.VerificationCodeExpiresAt is null || DateTimeOffset.UtcNow > share.VerificationCodeExpiresAt)
        {
            return VaultShareStatus.CodeExpired;
        }

        return VaultShareStatus.Pending;
    }

    private static VaultItemDto ToDto(VaultItem item) => new(item.Id, item.CiphertextBlob, item.CreatedAt, item.UpdatedAt);
}
