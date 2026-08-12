using EmergencyVault.Application.Dtos;

namespace EmergencyVault.Application.Interfaces;

public interface IAuthService
{
    Task<ServiceResult> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<ServiceResult<LoginResult>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct);
    Task<ServiceResult<AuthResult>> VerifyTwoFactorAsync(string challengeToken, string code, string? ipAddress, CancellationToken ct);
    Task<ServiceResult> ResendTwoFactorCodeAsync(string challengeToken, CancellationToken ct);
    Task<ServiceResult<AuthResult>> RefreshAsync(string rawRefreshToken, string? ipAddress, CancellationToken ct);
    Task<ServiceResult> LogoutAsync(string rawRefreshToken, CancellationToken ct);
    Task<ServiceResult> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct);
    Task<ServiceResult> UnlockAccountAsync(Guid userId, string token, CancellationToken ct);
    Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken ct);
    Task<bool> GetTwoFactorStatusAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult> EnableTwoFactorAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult> DisableTwoFactorAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult> DeleteAccountAsync(Guid userId, CancellationToken ct);
}
