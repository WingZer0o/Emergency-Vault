using EmergencyVault.Application.Dtos;

namespace EmergencyVault.Application.Interfaces;

public interface IAuthService
{
    Task<ServiceResult> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<ServiceResult<LoginResult>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct);
    Task<ServiceResult<AuthResult>> VerifyTwoFactorAsync(string challengeToken, string code, string? ipAddress, CancellationToken ct);
    Task<ServiceResult<AuthResult>> VerifyTwoFactorRecoveryCodeAsync(string challengeToken, string recoveryCode, string? ipAddress, CancellationToken ct);
    Task<ServiceResult> ResendTwoFactorCodeAsync(string challengeToken, CancellationToken ct);
    Task<ServiceResult<AuthResult>> RefreshAsync(string rawRefreshToken, string? ipAddress, CancellationToken ct);
    Task<ServiceResult> LogoutAsync(string rawRefreshToken, CancellationToken ct);
    Task<ServiceResult> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct);
    Task<ServiceResult> UnlockAccountAsync(Guid userId, string token, CancellationToken ct);
    Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task<ServiceResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct);
    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken ct);
    Task<TwoFactorStatusResult> GetTwoFactorStatusAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult<TwoFactorActivationResult>> EnableTwoFactorAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult> DisableTwoFactorAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult<TotpEnrollmentResult>> StartTotpEnrollmentAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult<TwoFactorActivationResult>> ConfirmTotpEnrollmentAsync(Guid userId, string code, CancellationToken ct);
    Task<ServiceResult<RecoveryCodesResult>> RegenerateRecoveryCodesAsync(Guid userId, CancellationToken ct);
    Task<ServiceResult> DeleteAccountAsync(Guid userId, CancellationToken ct);
}
