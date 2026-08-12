namespace EmergencyVault.Application.Options;

public class Two2FACodesOptions
{
    public const string SectionName = "Two2FACodes";

    public int CodeLength { get; set; } = 6;
    public int CodeExpirationMinutes { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan TwoFactorChallengeTtl => TimeSpan.FromMinutes(CodeExpirationMinutes);
}