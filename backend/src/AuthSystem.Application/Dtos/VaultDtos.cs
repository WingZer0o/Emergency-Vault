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

public record CreateVaultItemRequest([Required] string CiphertextBlob);

public record UpdateVaultItemRequest([Required] string CiphertextBlob);

public record VaultItemDto(Guid Id, string CiphertextBlob, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record VaultItemAttachmentDto(
    Guid Id, Guid VaultItemId, string FileName, string ContentType, long SizeBytes, string Iv, DateTimeOffset CreatedAt);

public record VaultUserLookupDto(Guid UserId, string FirstName, string LastName, string PublicKey, string? MlKemPublicKey);

public record CreateVaultShareRequest(
    [Required, EmailAddress] string HolderEmail,
    [Required] string WrappedUvk);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VaultShareStatus { Pending, Active, CodeExpired, Failed }

public record VaultShareDto(Guid Id, Guid HolderUserId, string HolderName, string HolderEmail, DateTimeOffset GrantedAt, VaultShareStatus Status);

public record VaultSharedWithMeDto(Guid Id, Guid OwnerUserId, string OwnerName, string OwnerEmail, string? WrappedUvk, DateTimeOffset GrantedAt, VaultShareStatus Status);

public record VaultItemViewDto(Guid ViewerUserId, string ViewerName, string ViewerEmail, DateTimeOffset ViewedAt);

public record VerifyVaultShareRequest([Required, StringLength(6, MinimumLength = 6)] string Code);

public record SharedVaultAccessChallengeDto(bool RequiresCode, string? ChallengeToken);

public record VerifySharedVaultAccessRequest(
    [Required] string ChallengeToken,
    [Required, StringLength(6, MinimumLength = 6)] string Code);

public record ResendSharedVaultAccessRequest([Required] string ChallengeToken);
