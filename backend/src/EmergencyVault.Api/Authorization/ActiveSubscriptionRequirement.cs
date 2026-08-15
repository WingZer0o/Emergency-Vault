using Microsoft.AspNetCore.Authorization;

namespace EmergencyVault.Api.Authorization;

/// <summary>Hard-paywall gate: satisfied by an Admin role, an active paid subscription, or an admin-granted free-access flag.</summary>
public class ActiveSubscriptionRequirement : IAuthorizationRequirement;
