using EmergencyVault.Application.Dtos;

namespace EmergencyVault.Application.Interfaces;

public interface IBillingService
{
    Task<BillingStatusDto> GetStatusAsync(Guid userId, CancellationToken ct);
    Task<CheckoutSessionDto> CreateCheckoutSessionAsync(Guid userId, CancellationToken ct);

    /// <summary>Throws InvalidOperationException if the user has no Stripe customer yet (never completed a checkout).</summary>
    Task<CheckoutSessionDto> CreatePortalSessionAsync(Guid userId, CancellationToken ct);

    Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken ct);

    Task<BillingSettingsDto> GetSettingsAsync(CancellationToken ct);
    Task<BillingSettingsDto> UpdateSettingsAsync(UpdateBillingSettingsRequest request, CancellationToken ct);

    Task<IReadOnlyList<AdminSubscriptionRowDto>> ListSubscriptionsAsync(CancellationToken ct);
    Task SetFreeAccessAsync(Guid userId, bool isFreeAccess, CancellationToken ct);
}
