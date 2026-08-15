using EmergencyVault.Application.Dtos;
using EmergencyVault.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmergencyVault.Api.Controllers;

[ApiController]
[Route("api/admin/billing")]
[Authorize(Roles = "Admin")]
public class AdminBillingController(IBillingService billingService) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await billingService.GetSettingsAsync(ct);
        return Ok(settings);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(UpdateBillingSettingsRequest request, CancellationToken ct)
    {
        var settings = await billingService.UpdateSettingsAsync(request, ct);
        return Ok(settings);
    }

    [HttpGet("subscriptions")]
    public async Task<IActionResult> ListSubscriptions(CancellationToken ct)
    {
        var subscriptions = await billingService.ListSubscriptionsAsync(ct);
        return Ok(subscriptions);
    }

    [HttpPut("subscriptions/{userId:guid}/free-access")]
    public async Task<IActionResult> SetFreeAccess(Guid userId, SetFreeAccessRequest request, CancellationToken ct)
    {
        await billingService.SetFreeAccessAsync(userId, request.IsFreeAccess, ct);
        return NoContent();
    }
}
