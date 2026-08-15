using System.Text.Json.Serialization;

namespace EmergencyVault.Domain.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubscriptionStatus
{
    None = 0,
    Active = 1,
    PastDue = 2,
    Canceled = 3,
}

/// <summary>
/// One row per user tracking their yearly-membership entitlement - either a paid Stripe
/// subscription or an admin-granted free-access grant. <see cref="IsFreeAccess"/> always wins
/// over <see cref="Status"/>, regardless of what Stripe reports.
/// </summary>
public class Subscription
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.None;

    /// <summary>End of the current paid period, as reported by Stripe. Null until a subscription has been created.</summary>
    public DateTimeOffset? CurrentPeriodEnd { get; set; }

    /// <summary>Admin-granted free access, set from the Admin CMS. Bypasses the paywall regardless of Stripe status.</summary>
    public bool IsFreeAccess { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
