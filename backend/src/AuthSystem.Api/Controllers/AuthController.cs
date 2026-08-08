using AuthSystem.Api.Extensions;
using AuthSystem.Application.Dtos;
using AuthSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshCookieName = "refreshToken";

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

        SetRefreshCookie(result.Data.RefreshToken, result.Data.RefreshTokenExpiresAt);
        return Ok(ToAuthResponse(result.Data));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var rawToken = Request.Cookies[RefreshCookieName] ?? string.Empty;
        var result = await authService.RefreshAsync(rawToken, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (!result.Succeeded || result.Data is null)
        {
            Response.Cookies.Delete(RefreshCookieName);
            return Unauthorized(new { errors = result.Errors });
        }

        SetRefreshCookie(result.Data.RefreshToken, result.Data.RefreshTokenExpiresAt);
        return Ok(ToAuthResponse(result.Data));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var rawToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(rawToken))
        {
            await authService.LogoutAsync(rawToken, ct);
        }

        Response.Cookies.Delete(RefreshCookieName);
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
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var profile = await authService.GetProfileAsync(User.GetUserId(), ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    private void SetRefreshCookie(string token, DateTimeOffset expiresAt)
    {
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
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
        accessToken = result.AccessToken,
        accessTokenExpiresAt = result.AccessTokenExpiresAt,
        user = result.User,
    };
}
