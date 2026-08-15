using EmergencyVault.Api.Extensions;
using EmergencyVault.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmergencyVault.Api.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize]
public class BillingController(IBillingService billingService, ILogger<BillingController> logger) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await billingService.GetStatusAsync(User.GetUserId(), ct);
        return Ok(status);
    }

    [HttpPost("checkout-session")]
    public async Task<IActionResult> CreateCheckoutSession(CancellationToken ct)
    {
        var session = await billingService.CreateCheckoutSessionAsync(User.GetUserId(), ct);
        return Ok(session);
    }

    /// <summary>Stripe-hosted portal where a user can update their card or cancel, without us building either flow.</summary>
    [HttpPost("portal-session")]
    public async Task<IActionResult> CreatePortalSession(CancellationToken ct)
    {
        try
        {
            var session = await billingService.CreatePortalSessionAsync(User.GetUserId(), ct);
            return Ok(session);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { errors = new[] { "No billing history for this account yet." } });
        }
    }

    /// <summary>
    /// Stripe webhook receiver. Reads the raw request body directly (no model binding) because
    /// signature verification requires the exact bytes Stripe sent.
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            await billingService.HandleWebhookAsync(payload, signature, ct);
            return Ok();
        }
        catch (Stripe.StripeException ex)
        {
            logger.LogWarning(ex, "Stripe webhook rejected: {Message}", ex.Message);
            return BadRequest();
        }
    }
}
