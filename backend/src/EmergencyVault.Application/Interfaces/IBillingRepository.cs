using EmergencyVault.Domain.Entities;

namespace EmergencyVault.Application.Interfaces;

/// <summary>EF Core-backed repository for the billing feature's entities - mirrors IVaultRepository's thin style.</summary>
public interface IBillingRepository
{
    Task<Subscription?> GetSubscriptionAsync(Guid userId, CancellationToken ct);
    Task<Subscription?> GetSubscriptionByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken ct);
    Task<IReadOnlyList<Subscription>> GetAllSubscriptionsAsync(CancellationToken ct);
    Task AddSubscriptionAsync(Subscription subscription, CancellationToken ct);

    Task<BillingSetting> GetSettingsAsync(CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
