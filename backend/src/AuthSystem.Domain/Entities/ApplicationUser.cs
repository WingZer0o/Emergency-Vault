using Microsoft.AspNetCore.Identity;

namespace AuthSystem.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Base64-encoded HMAC key backing this user's email HOTP codes (RFC 4226). Null until 2FA is enabled.</summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>Next HOTP counter value to consume; advances by one every time a code is generated.</summary>
    public long TwoFactorCounter { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
