using EmergencyVault.Application.Dtos;

namespace EmergencyVault.Application.Interfaces;

public interface IBillingService
{
    Task<BillingStatusDto> GetStatusAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Same shape as GetStatusAsync, but first reconciles against Stripe directly rather than
    /// relying on webhook delivery having already caught up (or arrived at all). Meant for the
    /// billing page specifically (initial load, "Refresh status") - never called from
    /// GetStatusAsync/ActiveSubscriptionHandler, which runs on every vault request and can't afford
    /// a live Stripe round trip that often.
    /// </summary>
    Task<BillingStatusDto> SyncStatusAsync(Guid userId, CancellationToken ct);

    Task<EmbeddedCheckoutSessionDto> CreateCheckoutSessionAsync(Guid userId, bool isDesktop, CancellationToken ct);

    /// <summary>Throws InvalidOperationException if the user has no Stripe customer yet (never completed a checkout).</summary>
    Task<SetupIntentDto> CreateSetupIntentAsync(Guid userId, CancellationToken ct);

    /// <summary>Throws InvalidOperationException if the user has no Stripe customer yet. Makes the payment method the default for future invoices.</summary>
    Task SetDefaultPaymentMethodAsync(Guid userId, string paymentMethodId, CancellationToken ct);

    /// <summary>Publishable (non-secret) key the frontend needs to load Stripe.js and mount the embedded Checkout/Payment Element iframes.</summary>
    PublishableKeyDto GetPublishableKey();

    Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken ct);

    Task<BillingSettingsDto> GetSettingsAsync(CancellationToken ct);
    Task<BillingSettingsDto> UpdateSettingsAsync(UpdateBillingSettingsRequest request, CancellationToken ct);

    Task<IReadOnlyList<AdminSubscriptionRowDto>> ListSubscriptionsAsync(CancellationToken ct);
    Task SetFreeAccessAsync(Guid userId, bool isFreeAccess, CancellationToken ct);

    /// <summary>
    /// Cancels any active/past-due Stripe subscription on file for this user. No-op if they never
    /// had one. Used both for self-serve cancellation (access still runs out at CurrentPeriodEnd,
    /// same as the portal's "cancel at period end" - see HasActiveEntitlement) and for account
    /// deletion, where Stripe must be told to stop billing the user before their local data is gone.
    /// </summary>
    Task CancelSubscriptionAsync(Guid userId, CancellationToken ct);
}
