using EmergencyVault.Application.Interfaces;
using EmergencyVault.Domain.Entities;
using EmergencyVault.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EmergencyVault.Infrastructure.Repositories;

public class BillingRepository(AppDbContext db) : IBillingRepository
{
    public async Task<Subscription?> GetSubscriptionAsync(Guid userId, CancellationToken ct)
    {
        return await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId, ct);
    }

    public async Task<Subscription?> GetSubscriptionByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken ct)
    {
        return await db.Subscriptions.FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, ct);
    }

    public async Task<IReadOnlyList<Subscription>> GetAllSubscriptionsAsync(CancellationToken ct)
    {
        return await db.Subscriptions.ToListAsync(ct);
    }

    public async Task AddSubscriptionAsync(Subscription subscription, CancellationToken ct)
    {
        await db.Subscriptions.AddAsync(subscription, ct);
    }

    public async Task<BillingSetting> GetSettingsAsync(CancellationToken ct)
    {
        return await db.BillingSettings.FirstAsync(b => b.Id == BillingSetting.SingletonId, ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
