using AuthSystem.Api.Extensions;
using AuthSystem.Application.Dtos;
using AuthSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthSystem.Api.Controllers;

[ApiController]
[Route("api/vault")]
[Authorize]
public class VaultController(IVaultService vaultService) : ControllerBase
{
    [HttpPost("setup")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Setup(VaultSetupRequest request, CancellationToken ct)
    {
        var result = await vaultService.SetupAsync(User.GetUserId(), request, ct);
        return result.Succeeded
            ? Created(string.Empty, new { message = "Vault set up successfully." })
            : Conflict(new { errors = result.Errors });
    }

    [HttpGet("status")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await vaultService.GetStatusAsync(User.GetUserId(), ct);
        return Ok(status);
    }

    [HttpGet("unlock-bundle")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> UnlockBundle(CancellationToken ct)
    {
        var result = await vaultService.GetUnlockBundleAsync(User.GetUserId(), ct);
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    [HttpPost("recover")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Recover(VaultRecoverRequest request, CancellationToken ct)
    {
        var rewrap = new VaultRewrapDto(request.NewKdfSalt, request.NewKdfIterations, request.NewPrivateKeyWrappedByMasterKey);
        var result = await vaultService.RewrapMasterKeyAsync(User.GetUserId(), rewrap, ct);
        return result.Succeeded
            ? Ok(new { message = "Vault access restored." })
            : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("reset-recovery-bundle")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> ResetRecoveryBundle([FromQuery] Guid userId, [FromQuery] string token, CancellationToken ct)
    {
        var result = await vaultService.GetResetRecoveryBundleAsync(userId, token, ct);
        return result.Succeeded ? Ok(result.Data) : NotFound();
    }

    [HttpPost("items")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> CreateItem(CreateVaultItemRequest request, CancellationToken ct)
    {
        var result = await vaultService.CreateItemAsync(User.GetUserId(), request, ct);
        return result.Succeeded ? Created(string.Empty, result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("items")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetItems(CancellationToken ct)
    {
        var result = await vaultService.GetItemsAsync(User.GetUserId(), ct);
        return Ok(result.Data);
    }

    [HttpGet("items/{id:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken ct)
    {
        var result = await vaultService.GetItemAsync(User.GetUserId(), id, ct);
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    [HttpPut("items/{id:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> UpdateItem(Guid id, UpdateVaultItemRequest request, CancellationToken ct)
    {
        var result = await vaultService.UpdateItemAsync(User.GetUserId(), id, request, ct);
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    [HttpDelete("items/{id:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
    {
        var result = await vaultService.DeleteItemAsync(User.GetUserId(), id, ct);
        return result.Succeeded ? NoContent() : NotFound(new { errors = result.Errors });
    }

    [HttpGet("lookup")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Lookup([FromQuery] string email, CancellationToken ct)
    {
        var result = await vaultService.LookupUserAsync(email, ct);
        return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
    }

    [HttpPost("shares")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> CreateShare(CreateVaultShareRequest request, CancellationToken ct)
    {
        var result = await vaultService.CreateShareAsync(User.GetUserId(), request, ct);
        return result.Succeeded ? Created(string.Empty, result.Data) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("shares")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> GetShares(CancellationToken ct)
    {
        var result = await vaultService.GetSharesAsync(User.GetUserId(), ct);
        return Ok(result.Data);
    }

    [HttpDelete("shares/{id:guid}")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> RevokeShare(Guid id, CancellationToken ct)
    {
        var result = await vaultService.RevokeShareAsync(User.GetUserId(), id, ct);
        return result.Succeeded ? NoContent() : NotFound(new { errors = result.Errors });
    }

    [HttpPost("shares/{id:guid}/verify")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> VerifyShare(Guid id, VerifyVaultShareRequest request, CancellationToken ct)
    {
        var result = await vaultService.VerifyShareAsync(User.GetUserId(), id, request.Code, ct);
        return result.Succeeded ? Ok(new { message = "Vault access verified." }) : BadRequest(new { errors = result.Errors });
    }

    [HttpPost("shares/{id:guid}/resend-code")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> ResendShareCode(Guid id, CancellationToken ct)
    {
        var result = await vaultService.ResendShareCodeAsync(User.GetUserId(), id, ct);
        return result.Succeeded ? Ok(new { message = "Code resent." }) : BadRequest(new { errors = result.Errors });
    }

    [HttpGet("shared-with-me")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> SharedWithMe(CancellationToken ct)
    {
        var result = await vaultService.GetSharedWithMeAsync(User.GetUserId(), ct);
        return Ok(result.Data);
    }

    [HttpGet("shared-with-me/{ownerUserId:guid}/items")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> SharedItems(Guid ownerUserId, CancellationToken ct)
    {
        var result = await vaultService.GetSharedItemsAsync(User.GetUserId(), ownerUserId, ct);
        return result.Succeeded ? Ok(result.Data) : StatusCode(StatusCodes.Status403Forbidden, new { errors = result.Errors });
    }
}
