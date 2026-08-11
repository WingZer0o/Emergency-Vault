using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AuthSystem.Application.Dtos;

public record VaultSetupRequest(
    [Required] string PublicKey,
    [Required] string KdfSalt,
    [Required, Range(200_000, 5_000_000)] int KdfIterations,
    [Required] string PrivateKeyWrappedByMasterKey,
    [Required] string PrivateKeyWrappedByRecoveryKey,
    [Required] string SelfWrappedUvk,
    [Required] string MlKemPublicKey,
    [Required] string MlKemSecretKeyWrappedByMasterKey,
    [Required] string MlKemSecretKeyWrappedByRecoveryKey);

public record VaultStatusDto(bool HasVault);

public record VaultUnlockBundleDto(
    string KdfSalt,
    int KdfIterations,
    string PrivateKeyWrappedByMasterKey,
    string PrivateKeyWrappedByRecoveryKey,
    string WrappedUvkSelf,
    string? MlKemPublicKey,
    string? MlKemSecretKeyWrappedByMasterKey,
    string? MlKemSecretKeyWrappedByRecoveryKey);

public record VaultRecoverRequest(
    [Required] string NewKdfSalt,
    [Required, Range(200_000, 5_000_000)] int NewKdfIterations,
    [Required] string NewPrivateKeyWrappedByMasterKey,
    string? NewMlKemSecretKeyWrappedByMasterKey);

public record VaultResetRecoveryBundleDto(string PrivateKeyWrappedByRecoveryKey, string? MlKemSecretKeyWrappedByRecoveryKey);

public record CreateVaultItemRequest([Required] string CiphertextBlob, [Required] string WrappedItemKey);

// WrappedItemKey is optional and only ever honored once, when the caller is the item's owner and
// the item doesn't already have one - see VaultService.UpdateItemAsync. It backfills a legacy item
// onto per-item-key encryption; it's never accepted from a share holder even with edit access.
public record UpdateVaultItemRequest([Required] string CiphertextBlob, string? WrappedItemKey);

public record VaultItemDto(
    Guid Id,
    string CiphertextBlob,
    // Null for a legacy item not yet migrated to per-item keys. This item's own key, wrapped under
    // the OWNER's UVK - safe to return to any caller since it's useless without that UVK.
    string? WrappedItemKey,
    // Only set when the caller's access came via an item-scoped share: that share's own copy of
    // this item's key, wrapped directly for the caller's public key.
    string? ItemShareWrappedKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record VaultItemAttachmentDto(
    Guid Id, Guid VaultItemId, string FileName, string ContentType, long SizeBytes, string Iv, DateTimeOffset CreatedAt);

public record VaultUserLookupDto(Guid UserId, string FirstName, string LastName, string PublicKey, string? MlKemPublicKey);

public record CreateVaultShareRequest(
    [Required, EmailAddress] string HolderEmail,
    [Required] string WrappedKey,
    // Null = whole-vault share (today's behavior). Set = scoped to just that one item.
    Guid? VaultItemId,
    // Ignored (forced false) unless VaultItemId is set - see VaultService.CreateShareAsync.
    bool CanEdit);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VaultShareStatus { Pending, Active, CodeExpired, Failed }

public record VaultShareDto(
    Guid Id, Guid HolderUserId, string HolderName, string HolderEmail,
    Guid? VaultItemId, bool CanEdit, DateTimeOffset GrantedAt, VaultShareStatus Status);

public record VaultSharedWithMeDto(Guid Id, Guid OwnerUserId, string OwnerName, string OwnerEmail, string? WrappedUvk, DateTimeOffset GrantedAt, VaultShareStatus Status);

/// <summary>An item-scoped share held by the current user - the holder's-eye view, analogous to
/// VaultSharedWithMeDto but for a single item rather than a whole vault.</summary>
public record VaultItemShareDto(
    Guid ShareId, Guid ItemId, Guid OwnerUserId, string OwnerName, string OwnerEmail,
    bool CanEdit, DateTimeOffset GrantedAt, VaultShareStatus Status);

public record VaultItemViewDto(Guid ViewerUserId, string ViewerName, string ViewerEmail, DateTimeOffset ViewedAt);

public record VerifyVaultShareRequest([Required, StringLength(6, MinimumLength = 6)] string Code);

public record SharedVaultAccessChallengeDto(bool RequiresCode, string? ChallengeToken);

public record VerifySharedVaultAccessRequest(
    [Required] string ChallengeToken,
    [Required, StringLength(6, MinimumLength = 6)] string Code);

public record ResendSharedVaultAccessRequest([Required] string ChallengeToken);
