using EmergencyVault.Api.Extensions;
using EmergencyVault.Application.Dtos;
using EmergencyVault.Application.Interfaces;
using EmergencyVault.Domain.Constant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmergencyVault.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await authService.RegisterAsync(request, ct);
        return result.Succeeded
            ? Ok(new { message = "Registration successful. Check your email to confirm your account." })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (!result.Succeeded || result.Data is null)
        {
            return Unauthorized(new { errors = result.Errors });
        }

        if (result.Data.RequiresTwoFactor)
        {
            return Ok(new { requiresTwoFactor = true, twoFactorToken = result.Data.TwoFactorToken });
        }

        SetRefreshCookie(result.Data.Auth!.RefreshToken, result.Data.Auth.RefreshTokenExpiresAt);
        return Ok(ToAuthResponse(result.Data.Auth));
    }

    [HttpPost("login/two-factor")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyTwoFactor(VerifyTwoFactorRequest request, CancellationToken ct)
    {
        var result = await authService.VerifyTwoFactorAsync(
            request.TwoFactorToken, request.Code, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (!result.Succeeded || result.Data is null)
        {
            return Unauthorized(new { errors = result.Errors });
        }

        SetRefreshCookie(result.Data.RefreshToken, result.Data.RefreshTokenExpiresAt);
        return Ok(ToAuthResponse(result.Data));
    }

    [HttpPost("login/two-factor/resend")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendTwoFactor(ResendTwoFactorRequest request, CancellationToken ct)
    {
        var result = await authService.ResendTwoFactorCodeAsync(request.TwoFactorToken, ct);
        return result.Succeeded
            ? Ok(new { message = "A new code has been sent." })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var rawToken = Request.Cookies[CookieConst.RefreshCookieName] ?? string.Empty;
        var result = await authService.RefreshAsync(rawToken, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (!result.Succeeded || result.Data is null)
        {
            Response.Cookies.Delete(CookieConst.RefreshCookieName);
            return Unauthorized(new { errors = result.Errors });
        }

        SetRefreshCookie(result.Data.RefreshToken, result.Data.RefreshTokenExpiresAt);
        return Ok(ToAuthResponse(result.Data));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var rawToken = Request.Cookies[CookieConst.RefreshCookieName];
        if (!string.IsNullOrEmpty(rawToken))
        {
            await authService.LogoutAsync(rawToken, ct);
        }

        Response.Cookies.Delete(CookieConst.RefreshCookieName);
        return NoContent();
    }

    [HttpGet("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token, CancellationToken ct)
    {
        var result = await authService.ConfirmEmailAsync(userId, token, ct);
        return result.Succeeded
            ? Ok(new { message = "Email confirmed. You can now log in." })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("unlock-account")]
    [AllowAnonymous]
    public async Task<IActionResult> UnlockAccount([FromQuery] Guid userId, [FromQuery] string token, CancellationToken ct)
    {
        var result = await authService.UnlockAccountAsync(userId, token, ct);
        return result.Succeeded
            ? Ok(new { message = "Account unlocked. You can now log in." })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await authService.ForgotPasswordAsync(request, ct);
        // Always return 200 - do not reveal whether the email is registered.
        return Ok(new { message = "If that email is registered, a reset link has been sent." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await authService.ResetPasswordAsync(request, ct);
        return result.Succeeded
            ? Ok(new { message = "Password reset successful. You can now log in." })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("me")]
    [Authorize(Roles="User")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var profile = await authService.GetProfileAsync(User.GetUserId(), ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpDelete("account")]
    [Authorize(Roles="User")]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var result = await authService.DeleteAccountAsync(User.GetUserId(), ct);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        Response.Cookies.Delete(CookieConst.RefreshCookieName);
        return NoContent();
    }

    [HttpGet("two-factor/status")]
    [Authorize(Roles="User")]
    public async Task<IActionResult> TwoFactorStatus(CancellationToken ct)
    {
        var enabled = await authService.GetTwoFactorStatusAsync(User.GetUserId(), ct);
        return Ok(new { enabled });
    }

    [HttpPost("two-factor/enable")]
    [Authorize(Roles="User")]
    public async Task<IActionResult> EnableTwoFactor(CancellationToken ct)
    {
        var result = await authService.EnableTwoFactorAsync(User.GetUserId(), ct);
        return result.Succeeded
            ? Ok(new { message = "Two-factor authentication enabled." })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("two-factor/disable")]
    [Authorize(Roles="User")]
    public async Task<IActionResult> DisableTwoFactor(CancellationToken ct)
    {
        var result = await authService.DisableTwoFactorAsync(User.GetUserId(), ct);
        return result.Succeeded
            ? Ok(new { message = "Two-factor authentication disabled." })
            : BadRequest(new { errors = result.Errors });
    }

    private void SetRefreshCookie(string token, DateTimeOffset expiresAt)
    {
        Response.Cookies.Append(CookieConst.RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth",
            Expires = expiresAt,
        });
    }

    private static object ToAuthResponse(AuthResult result) => new
    {
        requiresTwoFactor = false,
        accessToken = result.AccessToken,
        accessTokenExpiresAt = result.AccessTokenExpiresAt,
        user = result.User,
    };
}
