using EmergencyVault.Domain.Entities;

namespace EmergencyVault.Application.Interfaces;

public enum StripeWebhookEventType
{
    Unknown,
    CheckoutCompleted,
    SubscriptionUpdated,
    SubscriptionDeleted,
    PaymentFailed,
}

/// <summary>
/// A Stripe webhook event, reduced to the fields BillingService needs. Keeps Stripe.net's own
/// types confined to the Infrastructure-layer StripeGateway implementation.
/// </summary>
public record ParsedStripeEvent(
    StripeWebhookEventType Type,
    string? CustomerId,
    string? SubscriptionId,
    Guid? UserId,
    SubscriptionStatus? Status,
    DateTimeOffset? CurrentPeriodEnd);

public interface IStripeGateway
{
    /// <summary>Reuses <paramref name="existingCustomerId"/> if present; otherwise creates a new Stripe Customer tagged with the userId in metadata.</summary>
    Task<string> GetOrCreateCustomerAsync(Guid userId, string email, string? existingCustomerId, CancellationToken ct);

    /// <summary>
    /// Creates a subscription-mode Checkout Session using inline price_data (yearly recurring) so
    /// admin-configured price changes take effect immediately without syncing a Stripe Price object.
    /// </summary>
    Task<string> CreateCheckoutSessionUrlAsync(
        Guid userId,
        string customerId,
        int unitAmountCents,
        string currency,
        string successUrl,
        string cancelUrl,
        CancellationToken ct);

    /// <summary>Creates a Stripe Billing Portal session so the customer can update their card or cancel, without us building either flow ourselves.</summary>
    Task<string> CreatePortalSessionUrlAsync(string customerId, string returnUrl, CancellationToken ct);

    ParsedStripeEvent ConstructWebhookEvent(string payload, string signatureHeader);
}
