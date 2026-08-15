using EmergencyVault.Application.Interfaces;
using EmergencyVault.Domain.Constant;
using EmergencyVault.Domain.Entities;
using Stripe;
using Stripe.Checkout;

namespace EmergencyVault.Infrastructure.Billing;

/// <summary>
/// Thin wrapper over the Stripe SDK - keeps Stripe.net's own types confined to this class so the
/// rest of the app only ever sees the Application-layer IStripeGateway/ParsedStripeEvent shapes.
/// </summary>
public class StripeGateway : IStripeGateway
{
    // Only needed by ConstructWebhookEvent - read lazily there rather than in the constructor.
    // StripeGateway is a dependency of BillingService, which in turn is a dependency of
    // ActiveSubscriptionHandler (runs on every VaultController request), so failing eagerly here
    // would take down the entire paywall/vault whenever the webhook secret simply hasn't been
    // configured yet (e.g. local dev before `stripe listen` has been run).
    public async Task<string> GetOrCreateCustomerAsync(Guid userId, string email, string? existingCustomerId, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(existingCustomerId))
        {
            return existingCustomerId;
        }

        var service = new CustomerService();
        var customer = await service.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Metadata = new Dictionary<string, string> { ["userId"] = userId.ToString() },
        }, cancellationToken: ct);

        return customer.Id;
    }

    public async Task<string> CreateCheckoutSessionUrlAsync(
        Guid userId,
        string customerId,
        int unitAmountCents,
        string currency,
        string successUrl,
        string cancelUrl,
        CancellationToken ct)
    {
        var service = new SessionService();
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = customerId,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency,
                        UnitAmount = unitAmountCents,
                        Recurring = new SessionLineItemPriceDataRecurringOptions { Interval = "year" },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "EmergencyVault Yearly Membership",
                        },
                    },
                },
            ],
            Metadata = new Dictionary<string, string> { ["userId"] = userId.ToString() },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { ["userId"] = userId.ToString() },
            },
        }, cancellationToken: ct);

        return session.Url;
    }

    public async Task<string> CreatePortalSessionUrlAsync(string customerId, string returnUrl, CancellationToken ct)
    {
        var service = new Stripe.BillingPortal.SessionService();
        var session = await service.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl,
        }, cancellationToken: ct);

        return session.Url;
    }

    public ParsedStripeEvent ConstructWebhookEvent(string payload, string signatureHeader)
    {
        var webhookSecret = Environment.GetEnvironmentVariable(EnvironmentVariableConst.StripeWebhookSecret)
            ?? throw new InvalidOperationException($"Missing {EnvironmentVariableConst.StripeWebhookSecret} environment variable.");

        // throwOnApiVersionMismatch: false - this Stripe account is still pinned to an older API
        // version than Stripe.net 52.3.0 expects. The fields BillingService actually reads
        // (customer/subscription ids, metadata, status, period end) have been stable across
        // Stripe API versions for years, so relaxing this check is safe for our narrow usage.
        var stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, webhookSecret, throwOnApiVersionMismatch: false);

        return stripeEvent.Type switch
        {
            "checkout.session.completed" => ParseCheckoutCompleted(stripeEvent),
            "customer.subscription.updated" => ParseSubscriptionEvent(stripeEvent, StripeWebhookEventType.SubscriptionUpdated),
            "customer.subscription.deleted" => ParseSubscriptionEvent(stripeEvent, StripeWebhookEventType.SubscriptionDeleted),
            "invoice.payment_failed" => ParseInvoiceFailed(stripeEvent),
            _ => new ParsedStripeEvent(StripeWebhookEventType.Unknown, null, null, null, null, null),
        };
    }

    private static ParsedStripeEvent ParseCheckoutCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Session
            ?? throw new InvalidOperationException("Unexpected payload for checkout.session.completed.");

        return new ParsedStripeEvent(
            StripeWebhookEventType.CheckoutCompleted,
            session.CustomerId,
            session.SubscriptionId,
            ParseUserId(session.Metadata),
            SubscriptionStatus.Active,
            null);
    }

    private static ParsedStripeEvent ParseSubscriptionEvent(Event stripeEvent, StripeWebhookEventType type)
    {
        var subscription = stripeEvent.Data.Object as Stripe.Subscription
            ?? throw new InvalidOperationException($"Unexpected payload for {stripeEvent.Type}.");

        var periodEndItem = subscription.Items.Data.FirstOrDefault();

        return new ParsedStripeEvent(
            type,
            subscription.CustomerId,
            subscription.Id,
            ParseUserId(subscription.Metadata),
            MapStatus(subscription.Status),
            periodEndItem is null ? null : new DateTimeOffset(periodEndItem.CurrentPeriodEnd, TimeSpan.Zero));
    }

    private static ParsedStripeEvent ParseInvoiceFailed(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice
            ?? throw new InvalidOperationException("Unexpected payload for invoice.payment_failed.");

        return new ParsedStripeEvent(
            StripeWebhookEventType.PaymentFailed,
            invoice.CustomerId,
            invoice.Parent?.SubscriptionDetails?.SubscriptionId,
            null,
            SubscriptionStatus.PastDue,
            null);
    }

    private static Guid? ParseUserId(IDictionary<string, string>? metadata)
    {
        if (metadata is not null && metadata.TryGetValue("userId", out var raw) && Guid.TryParse(raw, out var userId))
        {
            return userId;
        }

        return null;
    }

    private static SubscriptionStatus? MapStatus(string? stripeStatus) => stripeStatus switch
    {
        "active" or "trialing" => SubscriptionStatus.Active,
        "past_due" or "unpaid" or "incomplete" => SubscriptionStatus.PastDue,
        "canceled" or "incomplete_expired" => SubscriptionStatus.Canceled,
        _ => null,
    };
}
