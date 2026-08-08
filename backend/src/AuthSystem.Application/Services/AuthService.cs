using AuthSystem.Application.Dtos;
using AuthSystem.Application.Interfaces;
using AuthSystem.Application.Options;
using AuthSystem.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthSystem.Application.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokenRepository,
    IRefreshTokenCache refreshTokenCache,
    IEmailSender emailSender,
    IOptions<JwtOptions> jwtOptions,
    IOptions<FrontendOptions> frontendOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly FrontendOptions _frontendOptions = frontendOptions.Value;

    public async Task<ServiceResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            return ServiceResult.Failure("Email is already registered.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return ServiceResult.Failure(createResult.Errors.Select(e => e.Description).ToArray());
        }

        await userManager.AddToRoleAsync(user, "User");

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = $"{_frontendOptions.BaseUrl}/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await emailSender.SendEmailConfirmationAsync(user.Email!, link, ct);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<AuthResult>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return ServiceResult<AuthResult>.Failure("Invalid email or password.");
        }

        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            return ServiceResult<AuthResult>.Failure("Email has not been confirmed yet.");
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            var message = signInResult.IsLockedOut
                ? "Account is temporarily locked due to repeated failed attempts."
                : "Invalid email or password.";
            return ServiceResult<AuthResult>.Failure(message);
        }

        var result = await IssueTokenPairAsync(user, ipAddress, ct);
        return ServiceResult<AuthResult>.Success(result);
    }

    public async Task<ServiceResult<AuthResult>> RefreshAsync(string rawRefreshToken, string? ipAddress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return ServiceResult<AuthResult>.Failure("Missing refresh token.");
        }

        var hash = tokenService.HashToken(rawRefreshToken);
        var stored = await refreshTokenRepository.GetByTokenHashAsync(hash, ct);
        if (stored is null || !stored.IsActive)
        {
            return ServiceResult<AuthResult>.Failure("Invalid or expired refresh token.");
        }

        var user = stored.User ?? await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null)
        {
            return ServiceResult<AuthResult>.Failure("Invalid or expired refresh token.");
        }

        var newRaw = tokenService.GenerateRefreshToken();
        stored.RevokedAt = DateTimeOffset.UtcNow;
        stored.RevokedByIp = ipAddress;
        stored.ReplacedByTokenHash = newRaw.Hash;

        await refreshTokenCache.RemoveAsync(hash, ct);

        var result = await IssueTokenPairAsync(user, ipAddress, ct, newRaw);
        return ServiceResult<AuthResult>.Success(result);
    }

    public async Task<ServiceResult> LogoutAsync(string rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return ServiceResult.Success();
        }

        var hash = tokenService.HashToken(rawRefreshToken);
        var stored = await refreshTokenRepository.GetByTokenHashAsync(hash, ct);
        if (stored is not null && stored.IsActive)
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            stored.ReasonRevoked = "Logged out";
            await refreshTokenRepository.SaveChangesAsync(ct);
        }

        await refreshTokenCache.RemoveAsync(hash, ct);
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ConfirmEmailAsync(Guid userId, string token, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return ServiceResult.Failure("Invalid confirmation link.");
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return ServiceResult.Failure(result.Errors.Select(e => e.Description).ToArray());
        }

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            // Do not reveal whether the email exists.
            return ServiceResult.Success();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var link = $"{_frontendOptions.BaseUrl}/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await emailSender.SendPasswordResetAsync(user.Email!, link, ct);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
        {
            return ServiceResult.Failure("Invalid or expired reset link.");
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return ServiceResult.Failure(result.Errors.Select(e => e.Description).ToArray());
        }

        return ServiceResult.Success();
    }

    public async Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return new UserProfileDto(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList());
    }

    private async Task<AuthResult> IssueTokenPairAsync(
        ApplicationUser user, string? ipAddress, CancellationToken ct, RawRefreshToken? refreshToken = null)
    {
        var roles = await userManager.GetRolesAsync(user);
        var access = tokenService.GenerateAccessToken(user, roles.ToList());
        var refresh = refreshToken ?? tokenService.GenerateRefreshToken();

        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refresh.Hash,
            ExpiresAt = refresh.ExpiresAt,
            CreatedByIp = ipAddress,
        };

        await refreshTokenRepository.AddAsync(entity, ct);
        await refreshTokenRepository.SaveChangesAsync(ct);

        var ttl = refresh.ExpiresAt - DateTimeOffset.UtcNow;
        if (ttl > TimeSpan.Zero)
        {
            await refreshTokenCache.SetAsync(refresh.Hash, user.Id, ttl, ct);
        }

        logger.LogInformation("Issued token pair for user {UserId}", user.Id);

        return new AuthResult(
            access.Token,
            access.ExpiresAt,
            refresh.PlainText,
            refresh.ExpiresAt,
            new UserProfileDto(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList()));
    }
}
