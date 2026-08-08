using AuthSystem.Domain.Entities;

namespace AuthSystem.Application.Interfaces;

public record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
public record RawRefreshToken(string PlainText, string Hash, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessTokenResult GenerateAccessToken(ApplicationUser user, IReadOnlyList<string> roles);
    RawRefreshToken GenerateRefreshToken();
    string HashToken(string rawToken);
}
