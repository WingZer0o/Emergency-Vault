using System.ComponentModel.DataAnnotations;
using EmergencyVault.Domain.Enums;

namespace EmergencyVault.Application.Dtos;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string FirstName,
    [Required] string LastName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record ForgotPasswordRequest([Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [Required] Guid UserId,
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword,
    VaultRewrapDto? VaultRewrap = null);

/// <summary>
/// Optional companion to a password reset: re-wraps the vault private key (already decrypted
/// client-side using the recovery key) under a Master Key derived from the new password, so a
/// reset doesn't need the old password to preserve vault access. Absent = vault, if any, is left
/// locked until the user runs the standalone /vault/recover flow.
/// </summary>
public record VaultRewrapDto(
    [Required] string NewKdfSalt,
    [Required, Range(200_000, 5_000_000)] int NewKdfIterations,
    [Required] string NewPrivateKeyWrappedByMasterKey,
    string? NewMlKemSecretKeyWrappedByMasterKey);

/// <summary>Changes only the account sign-in password. The vault, if any, has its own independent
/// password and is changed separately via POST /api/vault/recover - see VaultController.</summary>
public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

public record UserProfileDto(Guid Id, string Email, string FirstName, string LastName, IReadOnlyList<string> Roles);

public record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfileDto User);

/// <summary>Result of a login attempt: either a completed sign-in, or a pending 2FA challenge.</summary>
public record LoginResult(bool RequiresTwoFactor, string? TwoFactorToken, TwoFactorMethod? TwoFactorMethod, AuthResult? Auth);

public record VerifyTwoFactorRequest(
    [Required] string TwoFactorToken,
    [Required, StringLength(6, MinimumLength = 6)] string Code);

public record ResendTwoFactorRequest([Required] string TwoFactorToken);

public record TwoFactorStatusResult(bool Enabled, TwoFactorMethod Method, int RecoveryCodesRemaining);

public record TotpEnrollmentResult(string Secret, string QrCodePng);

public record ConfirmTotpRequest([Required, StringLength(6, MinimumLength = 6)] string Code);

/// <summary>RecoveryCodes is only populated when a fresh batch was just generated - it's plaintext-visible exactly once.</summary>
public record TwoFactorActivationResult(IReadOnlyList<string>? RecoveryCodes);

public record RecoveryCodesResult(IReadOnlyList<string> Codes);

public record VerifyTwoFactorRecoveryRequest(
    [Required] string TwoFactorToken,
    [Required] string RecoveryCode);
