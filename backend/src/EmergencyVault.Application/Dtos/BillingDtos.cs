using System.ComponentModel.DataAnnotations;
using EmergencyVault.Domain.Entities;

namespace EmergencyVault.Application.Dtos;

public record BillingStatusDto(
    bool HasAccess,
    bool IsFreeAccess,
    SubscriptionStatus Status,
    DateTimeOffset? CurrentPeriodEnd,
    int YearlyPriceCents,
    string Currency,
    bool HasStripeCustomer);

public record CheckoutSessionDto(string Url);

public record BillingSettingsDto(int YearlyPriceCents, string Currency);

public record UpdateBillingSettingsRequest(
    [Required, Range(100, 1_000_000)] int YearlyPriceCents,
    [Required, StringLength(8, MinimumLength = 3)] string Currency);

public record AdminSubscriptionRowDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    SubscriptionStatus Status,
    DateTimeOffset? CurrentPeriodEnd,
    bool IsFreeAccess);

public record SetFreeAccessRequest([Required] bool IsFreeAccess);

// IsDesktop only ever picks between two server-controlled origins (see BillingService's
// DesktopReturnOrigin) - it can't be turned into an open redirect the way a caller-supplied
// URL could.
public record CreateCheckoutSessionRequest(bool IsDesktop);

public record CreatePortalSessionRequest(bool IsDesktop);
