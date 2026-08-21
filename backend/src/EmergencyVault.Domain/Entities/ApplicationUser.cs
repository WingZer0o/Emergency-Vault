using EmergencyVault.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace EmergencyVault.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Which second factor is currently active. Mutually exclusive - only one method is ever active.</summary>
    public TwoFactorMethod TwoFactorMethod { get; set; } = TwoFactorMethod.None;

    /// <summary>Base64-encoded HMAC key backing this user's email HOTP codes (RFC 4226). Null until email 2FA is enabled.</summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>Next HOTP counter value to consume; advances by one every time a code is generated.</summary>
    public long TwoFactorCounter { get; set; }

    /// <summary>Base32-encoded key backing this user's authenticator-app TOTP codes (RFC 6238). Set as soon as enrollment starts, but only live once TwoFactorMethod is Totp.</summary>
    public string? TotpSecret { get; set; }

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public virtual ICollection<UserFailedLogin> FailedLogins { get; set; } = new List<UserFailedLogin>();
}
