using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AuthSystem.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException("User id claim is missing or invalid.");
    }
}
