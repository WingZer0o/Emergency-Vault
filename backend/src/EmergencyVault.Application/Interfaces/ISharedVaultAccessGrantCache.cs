namespace EmergencyVault.Application.Interfaces;

/// <summary>
/// Marks that a holder has passed the per-view access challenge for a given owner's shared vault
/// within the current session window. Presence (not the value) is what matters - a hit means no
/// further code is required until the TTL set at grant time expires.
/// </summary>
public interface ISharedVaultAccessGrantCache
{
    Task GrantAsync(Guid holderUserId, Guid ownerUserId, TimeSpan ttl, CancellationToken ct);
    Task<bool> HasValidGrantAsync(Guid holderUserId, Guid ownerUserId, CancellationToken ct);
}
