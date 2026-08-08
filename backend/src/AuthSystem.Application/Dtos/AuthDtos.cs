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
