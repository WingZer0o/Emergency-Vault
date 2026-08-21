namespace EmergencyVault.Application.Interfaces;

public enum TwoFactorChallengeMethod
{
    Email,
    Totp,
}

/// <summary>
/// Pending 2FA challenge for a login in progress. Counter is only meaningful for Method Email -
/// it's the exact HOTP counter value the emailed code was generated with, so verification
/// recomputes the same code instead of trusting whatever the user submits. Totp challenges
/// verify against the user's TotpSecret directly and leave Counter at 0.
/// </summary>
public record TwoFactorChallenge(Guid UserId, TwoFactorChallengeMethod Method, long Counter, int Attempts);

public interface ITwoFactorChallengeCache
{
    Task SetAsync(string challengeToken, TwoFactorChallenge challenge, TimeSpan ttl, CancellationToken ct);
    Task<TwoFactorChallenge?> GetAsync(string challengeToken, CancellationToken ct);
    Task RemoveAsync(string challengeToken, CancellationToken ct);
}
