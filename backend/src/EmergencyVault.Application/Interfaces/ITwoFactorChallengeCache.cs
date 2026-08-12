namespace EmergencyVault.Application.Interfaces;

/// <summary>
/// Pending email-HOTP challenge for a login in progress. Counter is the exact HOTP counter
/// value the emailed code was generated with, so verification recomputes the same code
/// instead of trusting whatever the user submits.
/// </summary>
public record TwoFactorChallenge(Guid UserId, long Counter, int Attempts);

public interface ITwoFactorChallengeCache
{
    Task SetAsync(string challengeToken, TwoFactorChallenge challenge, TimeSpan ttl, CancellationToken ct);
    Task<TwoFactorChallenge?> GetAsync(string challengeToken, CancellationToken ct);
    Task RemoveAsync(string challengeToken, CancellationToken ct);
}
