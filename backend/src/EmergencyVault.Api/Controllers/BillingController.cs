using EmergencyVault.Api.Extensions;
using EmergencyVault.Application.Dtos;
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
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await billingService.GetStatusAsync(User.GetUserId(), ct);
        return Ok(status);
    }

    /// <summary>Same as Status, but reconciles against Stripe live first - see IBillingService.SyncStatusAsync.</summary>
    [HttpPost("sync-status")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> SyncStatus(CancellationToken ct)
    {
        var status = await billingService.SyncStatusAsync(User.GetUserId(), ct);
        return Ok(status);
    }

    [HttpPost("checkout-session")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request, CancellationToken ct)
    {
        var session = await billingService.CreateCheckoutSessionAsync(User.GetUserId(), request.IsDesktop, ct);
        return Ok(session);
    }

    /// <summary>Publishable (non-secret) key the frontend needs to load Stripe.js and mount the embedded Checkout iframe.</summary>
    [HttpGet("publishable-key")]
    [Authorize(Roles = "User")]
    public IActionResult GetPublishableKey() => Ok(billingService.GetPublishableKey());

    /// <summary>Embedded (Stripe.js Payment Element) equivalent of the portal's "update card" - see IStripeGateway.CreateSetupIntentAsync for why this exists instead of Stripe's hosted Billing Portal.</summary>
    [HttpPost("setup-intent")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> CreateSetupIntent(CancellationToken ct)
    {
        try
        {
            var intent = await billingService.CreateSetupIntentAsync(User.GetUserId(), ct);
            return Ok(intent);
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { errors = new[] { "No billing history for this account yet." } });
        }
    }

    [HttpPost("default-payment-method")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> SetDefaultPaymentMethod([FromBody] SetDefaultPaymentMethodRequest request, CancellationToken ct)
    {
        try
        {
            await billingService.SetDefaultPaymentMethodAsync(User.GetUserId(), request.PaymentMethodId, ct);
            return Ok();
        }
        catch (InvalidOperationException)
        {
            return BadRequest(new { errors = new[] { "No billing history for this account yet." } });
        }
    }

    /// <summary>Self-serve cancellation - see IBillingService.CancelSubscriptionAsync for how access is kept through the already-paid period.</summary>
    [HttpDelete("subscription")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> CancelSubscription(CancellationToken ct)
    {
        await billingService.CancelSubscriptionAsync(User.GetUserId(), ct);
        return Ok();
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
