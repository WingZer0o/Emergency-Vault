using EmergencyVault.Api.Extensions;
using EmergencyVault.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace EmergencyVault.Api.Authorization;

public class ActiveSubscriptionHandler(IBillingService billingService) : AuthorizationHandler<ActiveSubscriptionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveSubscriptionRequirement requirement)
    {
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.GetUserId();
        var status = await billingService.GetStatusAsync(userId, CancellationToken.None);

        if (status.HasAccess)
        {
            context.Succeed(requirement);
        }
    }
}
