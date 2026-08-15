namespace EmergencyVault.Domain.Entities;

/// <summary>
/// Singleton row (fixed Id = 1) holding the admin-configurable yearly membership price.
/// Read on every checkout-session creation so a price change takes effect immediately with no deploy.
/// </summary>
public class BillingSetting
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public int YearlyPriceCents { get; set; } = 5000;

    public string Currency { get; set; } = "usd";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
