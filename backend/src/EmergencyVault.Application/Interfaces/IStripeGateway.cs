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
    /// Creates a subscription-mode embedded Checkout Session using inline price_data (yearly
    /// recurring) so admin-configured price changes take effect immediately without syncing a
    /// Stripe Price object. "Embedded" means Stripe.js mounts the payment form as an iframe on our
    /// own /billing page rather than redirecting the browser to a Stripe-hosted page - returns the
    /// client secret Stripe.js needs to do that, not a URL.
    /// </summary>
    Task<string> CreateCheckoutSessionAsync(
        Guid userId,
        string customerId,
        int unitAmountCents,
        string currency,
        string returnUrl,
        CancellationToken ct);

    /// <summary>
    /// Creates a SetupIntent so the customer can securely add a new card via an embedded Stripe.js
    /// Payment Element, without us ever handling raw card data. Returns the client secret the
    /// frontend needs to mount it - Stripe's hosted Billing Portal (the alternative) can't be
    /// embedded in an iframe at all, which is why this exists instead of reusing that.
    /// </summary>
    Task<string> CreateSetupIntentAsync(string customerId, CancellationToken ct);

    /// <summary>Makes the given payment method the default for future invoices on the customer and, if present, their active subscription.</summary>
    Task SetDefaultPaymentMethodAsync(string customerId, string? subscriptionId, string paymentMethodId, CancellationToken ct);

    /// <summary>Cancels a Stripe subscription immediately (not at period end) so no further charges occur.</summary>
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct);

    ParsedStripeEvent ConstructWebhookEvent(string payload, string signatureHeader);
}
