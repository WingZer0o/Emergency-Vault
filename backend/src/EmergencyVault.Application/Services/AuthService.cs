using System.Security.Cryptography;
using System.Text;
using EmergencyVault.Application.Dtos;
using EmergencyVault.Application.Interfaces;
using EmergencyVault.Application.Options;
using EmergencyVault.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmergencyVault.Application.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ITokenService tokenService,
    IHotpService hotpService,
    ITwoFactorChallengeCache twoFactorChallengeCache,
    IRefreshTokenRepository refreshTokenRepository,
    IRefreshTokenCache refreshTokenCache,
    IUserFailedLoginRepository userFailedLoginRepository,
    IVaultRepository vaultRepository,
    IEmailSender emailSender,
    IVaultService vaultService,
    IBillingService billingService,
    IOptions<JwtOptions> jwtOptions,
    IOptions<FrontendOptions> frontendOptions,
    IOptions<Two2FACodesOptions> two2FACodesOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly FrontendOptions _frontendOptions = frontendOptions.Value;
    private readonly Two2FACodesOptions _two2FACodesOptions = two2FACodesOptions.Value;

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
        var link = $"{Environment.GetEnvironmentVariable("DOMAIN")}/confirm-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await emailSender.SendEmailConfirmationAsync(user.Email!, link, ct);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<LoginResult>> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            await RecordFailedLoginAsync(null, request.Email, "UserNotFound", ipAddress, ct);
            return ServiceResult<LoginResult>.Failure("Invalid email or password.");
        }

        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            return ServiceResult<LoginResult>.Failure("Email has not been confirmed yet.");
        }

        var wasLockedOut = await userManager.IsLockedOutAsync(user);
        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            var reason = signInResult.IsLockedOut ? "LockedOut" : "InvalidPassword";
            await RecordFailedLoginAsync(user.Id, request.Email, reason, ipAddress, ct);

            if (signInResult.IsLockedOut && !wasLockedOut)
            {
                await SendAccountLockedEmailAsync(user, ct);
            }

            var message = signInResult.IsLockedOut
                ? "Account is temporarily locked due to repeated failed attempts."
                : "Invalid email or password.";
            return ServiceResult<LoginResult>.Failure(message);
        }

        if (user.TwoFactorEnabled && !string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            var counter = await IssueTwoFactorCodeAsync(user, ct);
            var challengeToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            await twoFactorChallengeCache.SetAsync(challengeToken, new TwoFactorChallenge(user.Id, counter, 0), _two2FACodesOptions.TwoFactorChallengeTtl, ct);

            return ServiceResult<LoginResult>.Success(new LoginResult(true, challengeToken, null));
        }

        var result = await IssueTokenPairAsync(user, ipAddress, ct);
        return ServiceResult<LoginResult>.Success(new LoginResult(false, null, result));
    }

    public async Task<ServiceResult<AuthResult>> VerifyTwoFactorAsync(string challengeToken, string code, string? ipAddress, CancellationToken ct)
    {
        var challenge = await twoFactorChallengeCache.GetAsync(challengeToken, ct);
        if (challenge is null)
        {
            return ServiceResult<AuthResult>.Failure("Invalid or expired code. Please log in again.");
        }

        var user = await userManager.FindByIdAsync(challenge.UserId.ToString());
        if (user is null || !user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            await twoFactorChallengeCache.RemoveAsync(challengeToken, ct);
            return ServiceResult<AuthResult>.Failure("Invalid or expired code. Please log in again.");
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            await twoFactorChallengeCache.RemoveAsync(challengeToken, ct);
            return ServiceResult<AuthResult>.Failure("Account is temporarily locked due to repeated failed attempts.");
        }

        var secret = Convert.FromBase64String(user.TwoFactorSecret);
        var expectedCode = hotpService.GenerateCode(secret, challenge.Counter, _two2FACodesOptions.CodeLength);

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(code), Encoding.UTF8.GetBytes(expectedCode)))
        {
            await userManager.AccessFailedAsync(user);
            if (await userManager.IsLockedOutAsync(user))
            {
                await twoFactorChallengeCache.RemoveAsync(challengeToken, ct);
                await RecordFailedLoginAsync(user.Id, user.Email!, "LockedOut", ipAddress, ct);
                await SendAccountLockedEmailAsync(user, ct);
                return ServiceResult<AuthResult>.Failure("Account is temporarily locked due to repeated failed attempts.");
            }

            await RecordFailedLoginAsync(user.Id, user.Email!, "InvalidTwoFactorCode", ipAddress, ct);

            if (challenge.Attempts + 1 >= _two2FACodesOptions.MaxAttempts)
            {
                await twoFactorChallengeCache.RemoveAsync(challengeToken, ct);
                return ServiceResult<AuthResult>.Failure("Too many incorrect attempts. Please log in again.");
            }

            await twoFactorChallengeCache.SetAsync(
                challengeToken, challenge with { Attempts = challenge.Attempts + 1 }, _two2FACodesOptions.TwoFactorChallengeTtl, ct);
            return ServiceResult<AuthResult>.Failure("Incorrect code.");
        }

        await twoFactorChallengeCache.RemoveAsync(challengeToken, ct);
        await userManager.ResetAccessFailedCountAsync(user);
        var result = await IssueTokenPairAsync(user, ipAddress, ct);
        return ServiceResult<AuthResult>.Success(result);
    }

    public async Task<ServiceResult> ResendTwoFactorCodeAsync(string challengeToken, CancellationToken ct)
    {
        var challenge = await twoFactorChallengeCache.GetAsync(challengeToken, ct);
        if (challenge is null)
        {
            return ServiceResult.Failure("Invalid or expired code. Please log in again.");
        }

        var user = await userManager.FindByIdAsync(challenge.UserId.ToString());
        if (user is null || !user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            await twoFactorChallengeCache.RemoveAsync(challengeToken, ct);
            return ServiceResult.Failure("Invalid or expired code. Please log in again.");
        }

        var counter = await IssueTwoFactorCodeAsync(user, ct);
        await twoFactorChallengeCache.SetAsync(challengeToken, challenge with { Counter = counter, Attempts = 0 }, _two2FACodesOptions.TwoFactorChallengeTtl, ct);

        return ServiceResult.Success();
    }

    public async Task<bool> GetTwoFactorStatusAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user?.TwoFactorEnabled ?? false;
    }

    public async Task<ServiceResult> EnableTwoFactorAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return ServiceResult.Failure("User not found.");
        }

        if (string.IsNullOrEmpty(user.TwoFactorSecret))
        {
            user.TwoFactorSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(20));
            user.TwoFactorCounter = 0;
        }

        var result = await userManager.SetTwoFactorEnabledAsync(user, true);
        return result.Succeeded ? ServiceResult.Success() : ServiceResult.Failure(result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<ServiceResult> DisableTwoFactorAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return ServiceResult.Failure("User not found.");
        }

        user.TwoFactorSecret = null;
        user.TwoFactorCounter = 0;

        var result = await userManager.SetTwoFactorEnabledAsync(user, false);
        return result.Succeeded ? ServiceResult.Success() : ServiceResult.Failure(result.Errors.Select(e => e.Description).ToArray());
    }

    private async Task RecordFailedLoginAsync(Guid? userId, string email, string reason, string? ipAddress, CancellationToken ct)
    {
        await userFailedLoginRepository.AddAsync(new UserFailedLogin
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = email,
            Reason = reason,
            AttemptedByIp = ipAddress,
        }, ct);
        await userFailedLoginRepository.SaveChangesAsync(ct);
    }

    private async Task SendAccountLockedEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, "UnlockAccount");
        var link = $"{Environment.GetEnvironmentVariable("DOMAIN")}/unlock-account?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        await emailSender.SendAccountLockedAsync(user.Email!, link, ct);
    }

    public async Task<ServiceResult> UnlockAccountAsync(Guid userId, string token, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return ServiceResult.Failure("Invalid unlock link.");
        }

        var isValid = await userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultProvider, "UnlockAccount", token);
        if (!isValid)
        {
            return ServiceResult.Failure("Invalid or expired unlock link.");
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        return ServiceResult.Success();
    }

    /// <summary>Advances the user's HOTP counter, generates the code for it, emails it, and persists the new counter.</summary>
    private async Task<long> IssueTwoFactorCodeAsync(ApplicationUser user, CancellationToken ct)
    {
        user.TwoFactorCounter += 1;
        await userManager.UpdateAsync(user);

        var secret = Convert.FromBase64String(user.TwoFactorSecret!);
        var code = hotpService.GenerateCode(secret, user.TwoFactorCounter, _two2FACodesOptions.CodeLength);
        await emailSender.SendTwoFactorCodeAsync(user.Email!, code, ct);

        return user.TwoFactorCounter;
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
        var link = $"{Environment.GetEnvironmentVariable("DOMAIN")}/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";
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

        if (request.VaultRewrap is not null)
        {
            // Best-effort: a failed re-wrap (e.g. stale/missing vault) shouldn't block an
            // otherwise-successful password reset - the user can still recover vault access
            // separately via /vault/recover as long as they still have the recovery key.
            await vaultService.RewrapMasterKeyAsync(user.Id, request.VaultRewrap, ct);
        }

        return ServiceResult.Success();
    }

    public async Task<ServiceResult> DeleteAccountAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return ServiceResult.Failure("User not found.");
        }

        // Must run before UserManager.DeleteAsync: VaultProfile, owned VaultItems and granted
        // VaultKeyShares all cascade-delete with the user, but VaultKeyShare.Holder is a Restrict
        // FK (see AppDbContext), so any share this user holds - including their own self-share -
        // would otherwise block the delete with a FK constraint violation.
        await vaultRepository.DeleteHoldersSharesByIdAsync(userId, ct);

        // Stripe has no idea the account is about to disappear - cancel any active/past-due
        // subscription first so the user doesn't keep getting billed after their account is gone.
        // The Subscriptions row itself cascade-deletes with the user (see AppDbContext).
        await billingService.CancelSubscriptionAsync(userId, ct);

        var result = await userManager.DeleteAsync(user);
        return result.Succeeded ? ServiceResult.Success() : ServiceResult.Failure(result.Errors.Select(e => e.Description).ToArray());
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
