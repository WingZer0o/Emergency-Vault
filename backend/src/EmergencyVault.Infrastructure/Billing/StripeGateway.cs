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

    public async Task<string> CreateCheckoutSessionAsync(
        Guid userId,
        string customerId,
        int unitAmountCents,
        string currency,
        string returnUrl,
        CancellationToken ct)
    {
        var service = new SessionService();
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = customerId,
            // Stripe renamed this enum on 2026-03-25 ("embedded" -> "embedded_page"); the installed
            // @stripe/stripe-js only exposes the post-rename stripe.createEmbeddedCheckoutPage(...)
            // on the frontend, so the session must be created with the matching post-rename value.
            UiMode = "embedded_page",
            // "if_required" plus the frontend's onComplete callback keeps the whole flow inside our
            // own page (mounted via Stripe.js) for the common case - no top-level navigation away
            // from the app at all, which is what broke "back" navigation in the desktop app's
            // WebView (see BillingService's DesktopReturnOrigin comment). ReturnUrl only actually
            // gets used for the rare payment method that requires an out-of-band redirect (e.g. 3DS).
            RedirectOnCompletion = "if_required",
            ReturnUrl = returnUrl,
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

        return session.ClientSecret;
    }

    public async Task<string> CreateSetupIntentAsync(string customerId, CancellationToken ct)
    {
        var service = new SetupIntentService();
        var intent = await service.CreateAsync(new SetupIntentCreateOptions
        {
            Customer = customerId,
            Usage = "off_session",
            PaymentMethodTypes = ["card"],
        }, cancellationToken: ct);

        return intent.ClientSecret;
    }

    public async Task SetDefaultPaymentMethodAsync(string customerId, string? subscriptionId, string paymentMethodId, CancellationToken ct)
    {
        var customerService = new CustomerService();
        await customerService.UpdateAsync(customerId, new CustomerUpdateOptions
        {
            InvoiceSettings = new CustomerInvoiceSettingsOptions { DefaultPaymentMethod = paymentMethodId },
        }, cancellationToken: ct);

        // No active subscription yet (e.g. first card added before ever subscribing) - the customer
        // default set above is still enough for the next Checkout/invoice to pick it up.
        if (string.IsNullOrEmpty(subscriptionId))
        {
            return;
        }

        var subscriptionService = new SubscriptionService();
        await subscriptionService.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions
        {
            DefaultPaymentMethod = paymentMethodId,
        }, cancellationToken: ct);
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        var service = new SubscriptionService();
        await service.CancelAsync(subscriptionId, cancellationToken: ct);
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

        // A self-service cancellation via the billing portal schedules the cancellation (CanceledAt
        // gets set) but Stripe's own subscription.status stays "active" until the period actually
        // ends - relying on MapStatus alone would silently ignore the cancellation until then. Newer
        // API versions don't reliably set CancelAtPeriodEnd for this either, so CanceledAt being
        // non-null is the one signal that holds regardless of how the cancellation was scheduled.
        // Reflecting it as Canceled immediately is safe: HasActiveEntitlement already keeps a
        // Canceled subscription's access alive through CurrentPeriodEnd. A reverted cancellation
        // (resumed before period end) clears CanceledAt again, so this falls back to MapStatus and
        // correctly reports Active.
        var status = subscription.CanceledAt is not null ? SubscriptionStatus.Canceled : MapStatus(subscription.Status);

        return new ParsedStripeEvent(
            type,
            subscription.CustomerId,
            subscription.Id,
            ParseUserId(subscription.Metadata),
            status,
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
