namespace AuthSystem.Application.Options;

public class VaultShareCodesOptions
{
    public const string SectionName = "VaultShareCodes";

    public int CodeLength { get; set; } = 6;
    public int CodeExpirationHours { get; set; } = 72;
    public int MaxAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 60;
    public TimeSpan CodeExpiration => TimeSpan.FromHours(CodeExpirationHours);
    public TimeSpan ResendCooldown => TimeSpan.FromSeconds(ResendCooldownSeconds);
}
