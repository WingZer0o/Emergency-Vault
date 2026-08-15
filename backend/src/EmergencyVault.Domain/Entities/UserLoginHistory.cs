namespace EmergencyVault.Domain.Entities;

/// <summary>Audit trail of successful logins, used to detect sign-ins from an IP the user hasn't used before.</summary>
public class UserLoginHistory
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public string IpAddress { get; set; } = string.Empty;
    public DateTimeOffset LoggedInAt { get; set; } = DateTimeOffset.UtcNow;
}
