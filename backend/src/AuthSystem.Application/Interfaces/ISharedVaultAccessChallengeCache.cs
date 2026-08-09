namespace AuthSystem.Application.Interfaces;

/// <summary>
/// Pending email-HOTP challenge for viewing a vault shared with the holder. Counter is the exact
/// HOTP counter value the emailed code was generated with, so verification recomputes the same
/// code instead of trusting whatever the user submits. ShareId identifies which VaultKeyShare
/// row's VerificationSecret was used to generate the code.
/// </summary>
public record SharedVaultAccessChallenge(Guid HolderUserId, Guid OwnerUserId, Guid ShareId, long Counter, int Attempts, DateTimeOffset SentAt);

public interface ISharedVaultAccessChallengeCache
{
    Task SetAsync(string challengeToken, SharedVaultAccessChallenge challenge, TimeSpan ttl, CancellationToken ct);
    Task<SharedVaultAccessChallenge?> GetAsync(string challengeToken, CancellationToken ct);
    Task RemoveAsync(string challengeToken, CancellationToken ct);
}
