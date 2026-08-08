using System.ComponentModel.DataAnnotations;

namespace AuthSystem.Application.Dtos;

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
    [Required, MinLength(8)] string NewPassword);

public record UserProfileDto(Guid Id, string Email, string FirstName, string LastName, IReadOnlyList<string> Roles);

public record AuthResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfileDto User);

/// <summary>Result of a login attempt: either a completed sign-in, or a pending email-2FA challenge.</summary>
public record LoginResult(bool RequiresTwoFactor, string? TwoFactorToken, AuthResult? Auth);

public record VerifyTwoFactorRequest(
    [Required] string TwoFactorToken,
    [Required, StringLength(6, MinimumLength = 6)] string Code);

public record ResendTwoFactorRequest([Required] string TwoFactorToken);
