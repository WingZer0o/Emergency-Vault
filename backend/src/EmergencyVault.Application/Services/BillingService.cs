using EmergencyVault.Application.Dtos;
using EmergencyVault.Application.Interfaces;
using EmergencyVault.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EmergencyVault.Application.Services;

public class BillingService(
    IBillingRepository repository,
    IStripeGateway stripeGateway,
    UserManager<ApplicationUser> userManager) : IBillingService
{
    public async Task<BillingStatusDto> GetStatusAsync(Guid userId, CancellationToken ct)
    {
        var subscription = await repository.GetSubscriptionAsync(userId, ct);
        var settings = await repository.GetSettingsAsync(ct);

        return new BillingStatusDto(
            subscription is not null && HasActiveEntitlement(subscription),
            subscription?.IsFreeAccess ?? false,
            subscription?.Status ?? SubscriptionStatus.None,
            subscription?.CurrentPeriodEnd,
            settings.YearlyPriceCents,
            settings.Currency,
            !string.IsNullOrEmpty(subscription?.StripeCustomerId));
    }

    public async Task<CheckoutSessionDto> CreateCheckoutSessionAsync(Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        var subscription = await GetOrCreateSubscriptionAsync(userId, ct);
        var settings = await repository.GetSettingsAsync(ct);

        var customerId = await stripeGateway.GetOrCreateCustomerAsync(userId, user.Email!, subscription.StripeCustomerId, ct);
        if (subscription.StripeCustomerId != customerId)
        {
            subscription.StripeCustomerId = customerId;
            subscription.UpdatedAt = DateTimeOffset.UtcNow;
            await repository.SaveChangesAsync(ct);
        }

        // Built server-side from the DOMAIN environment variable (same source AuthService uses for
        // its email links) rather than accepted from the client, to avoid an open-redirect via a
        // caller-supplied success/cancel URL.
        var domain = Environment.GetEnvironmentVariable("DOMAIN");
        var successUrl = $"{domain}/billing?success=1";
        var cancelUrl = $"{domain}/billing?canceled=1";

        var url = await stripeGateway.CreateCheckoutSessionUrlAsync(
            userId, customerId, settings.YearlyPriceCents, settings.Currency, successUrl, cancelUrl, ct);

        return new CheckoutSessionDto(url);
    }

    public async Task<CheckoutSessionDto> CreatePortalSessionAsync(Guid userId, CancellationToken ct)
    {
        var subscription = await repository.GetSubscriptionAsync(userId, ct);
        if (string.IsNullOrEmpty(subscription?.StripeCustomerId))
        {
            throw new InvalidOperationException("No billing history for this user yet.");
        }

        var returnUrl = $"{Environment.GetEnvironmentVariable("DOMAIN")}/billing";
        var url = await stripeGateway.CreatePortalSessionUrlAsync(subscription.StripeCustomerId, returnUrl, ct);
        return new CheckoutSessionDto(url);
    }

    public async Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken ct)
    {
        var evt = stripeGateway.ConstructWebhookEvent(payload, signatureHeader);

        switch (evt.Type)
        {
            case StripeWebhookEventType.CheckoutCompleted when evt.UserId is not null:
            {
                var subscription = await GetOrCreateSubscriptionAsync(evt.UserId.Value, ct);
                subscription.StripeCustomerId = evt.CustomerId ?? subscription.StripeCustomerId;
                subscription.StripeSubscriptionId = evt.SubscriptionId ?? subscription.StripeSubscriptionId;
                subscription.Status = SubscriptionStatus.Active;
                subscription.CurrentPeriodEnd = evt.CurrentPeriodEnd ?? subscription.CurrentPeriodEnd;
                subscription.UpdatedAt = DateTimeOffset.UtcNow;
                await repository.SaveChangesAsync(ct);
                break;
            }
            case StripeWebhookEventType.SubscriptionUpdated:
            case StripeWebhookEventType.SubscriptionDeleted:
            case StripeWebhookEventType.PaymentFailed:
            {
                // These events describe an existing Stripe subscription, so matching by
                // StripeSubscriptionId (persisted during CheckoutCompleted above) is more robust
                // than re-deriving the user from metadata.
                if (evt.SubscriptionId is null)
                {
                    break;
                }

                var subscription = await repository.GetSubscriptionByStripeSubscriptionIdAsync(evt.SubscriptionId, ct);
                if (subscription is null)
                {
                    break;
                }

                subscription.Status = evt.Type switch
                {
                    StripeWebhookEventType.SubscriptionDeleted => SubscriptionStatus.Canceled,
                    StripeWebhookEventType.PaymentFailed => SubscriptionStatus.PastDue,
                    _ => evt.Status ?? subscription.Status,
                };
                if (evt.CurrentPeriodEnd is not null)
                {
                    subscription.CurrentPeriodEnd = evt.CurrentPeriodEnd;
                }
                subscription.UpdatedAt = DateTimeOffset.UtcNow;
                await repository.SaveChangesAsync(ct);
                break;
            }
        }
    }

    public async Task<BillingSettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        var settings = await repository.GetSettingsAsync(ct);
        return new BillingSettingsDto(settings.YearlyPriceCents, settings.Currency);
    }

    public async Task<BillingSettingsDto> UpdateSettingsAsync(UpdateBillingSettingsRequest request, CancellationToken ct)
    {
        var settings = await repository.GetSettingsAsync(ct);
        settings.YearlyPriceCents = request.YearlyPriceCents;
        settings.Currency = request.Currency.ToLowerInvariant();
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(ct);
        return new BillingSettingsDto(settings.YearlyPriceCents, settings.Currency);
    }

    public async Task<IReadOnlyList<AdminSubscriptionRowDto>> ListSubscriptionsAsync(CancellationToken ct)
    {
        var users = await userManager.Users.OrderBy(u => u.Email).ToListAsync(ct);
        var subscriptionsByUserId = (await repository.GetAllSubscriptionsAsync(ct)).ToDictionary(s => s.UserId);

        return users.Select(user =>
        {
            subscriptionsByUserId.TryGetValue(user.Id, out var subscription);
            return new AdminSubscriptionRowDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                subscription?.Status ?? SubscriptionStatus.None,
                subscription?.CurrentPeriodEnd,
                subscription?.IsFreeAccess ?? false);
        }).ToList();
    }

    public async Task SetFreeAccessAsync(Guid userId, bool isFreeAccess, CancellationToken ct)
    {
        var subscription = await GetOrCreateSubscriptionAsync(userId, ct);

        // Granting free access replaces a paid subscription rather than sitting alongside it - cancel
        // any Stripe subscription still on file so the user isn't charged while free access is active.
        // Turning free access back off doesn't restore it: the user goes through checkout again, same
        // as anyone else without an active subscription.
        if (isFreeAccess
            && !string.IsNullOrEmpty(subscription.StripeSubscriptionId)
            && subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue)
        {
            await stripeGateway.CancelSubscriptionAsync(subscription.StripeSubscriptionId, ct);
            subscription.Status = SubscriptionStatus.Canceled;
        }

        subscription.IsFreeAccess = isFreeAccess;
        subscription.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(ct);
    }

    private static bool HasActiveEntitlement(Subscription subscription)
    {
        if (subscription.IsFreeAccess)
        {
            return true;
        }

        // Canceled still counts as entitled until CurrentPeriodEnd passes - the user already paid for
        // this period (whether they canceled themselves via the portal or an admin canceled it when
        // granting free access), so access should run out with the period, not the moment Status flips.
        // PastDue gets no such grace: a failed payment is not a period the user has paid for.
        if (subscription.Status is not (SubscriptionStatus.Active or SubscriptionStatus.Canceled))
        {
            return false;
        }

        return subscription.CurrentPeriodEnd is null || subscription.CurrentPeriodEnd > DateTimeOffset.UtcNow;
    }

    private async Task<Subscription> GetOrCreateSubscriptionAsync(Guid userId, CancellationToken ct)
    {
        var subscription = await repository.GetSubscriptionAsync(userId, ct);
        if (subscription is not null)
        {
            return subscription;
        }

        subscription = new Subscription { Id = Guid.NewGuid(), UserId = userId };
        await repository.AddSubscriptionAsync(subscription, ct);
        await repository.SaveChangesAsync(ct);
        return subscription;
    }
}
