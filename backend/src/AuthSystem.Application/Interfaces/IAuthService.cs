using AuthSystem.Application.Dtos;

namespace AuthSystem.Application.Interfaces;

public interface IAuthService
{
    Task<ServiceResult> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<ServiceResult<AuthResult>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct);
    Task<ServiceResult<AuthResult>> RefreshAsync(string rawRefreshToken, string? ipAddress, CancellationToken ct);
    Task<ServiceResult> LogoutAsync(string rawRefreshToken, CancellationToken ct);
    Task<ServiceResult> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct);
    Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken ct);
}
