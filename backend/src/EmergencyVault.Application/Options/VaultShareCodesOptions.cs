namespace EmergencyVault.Application.Options;

public class VaultShareCodesOptions
{
    public const string SectionName = "VaultShareCodes";

    public int CodeLength { get; set; } = 6;
    public int CodeExpirationHours { get; set; } = 72;
    public int MaxAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 60;
    public TimeSpan CodeExpiration => TimeSpan.FromHours(CodeExpirationHours);
    public TimeSpan ResendCooldown => TimeSpan.FromSeconds(ResendCooldownSeconds);

    /// <summary>Lifetime of a per-view access code - short, unlike the 72h invite-accept code above.</summary>
    public int AccessCodeExpirationMinutes { get; set; } = 10;
    public TimeSpan AccessCodeExpiration => TimeSpan.FromMinutes(AccessCodeExpirationMinutes);

    /// <summary>How long a passed access challenge is good for before the holder is re-prompted.</summary>
    public int SessionMinutes { get; set; } = 30;
    public TimeSpan SessionTtl => TimeSpan.FromMinutes(SessionMinutes);
}
