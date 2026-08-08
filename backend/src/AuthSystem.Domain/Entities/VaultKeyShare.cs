namespace AuthSystem.Domain.Entities;

/// <summary>
/// Maps a vault owner's User Vault Key (UVK), wrapped under a specific holder's public key, to
/// that holder. A row where VaultOwnerUserId == HolderUserId is the owner's own self-share,
/// created at vault setup - deliberately the same mechanism as an emergency-access grant, so
/// "unlock my own vault" and "an emergency contact opens my vault" are the same unwrap operation.
/// </summary>
public class VaultKeyShare
{
    public Guid Id { get; set; }

    /// <summary>Whose vault this grants access to.</summary>
    public Guid VaultOwnerUserId { get; set; }
    public ApplicationUser VaultOwner { get; set; } = null!;

    /// <summary>Who can unwrap it. Equal to VaultOwnerUserId for the self-share row.</summary>
    public Guid HolderUserId { get; set; }
    public ApplicationUser Holder { get; set; } = null!;

    /// <summary>base64 RSA-OAEP ciphertext of the raw UVK, wrapped under the holder's public key. No IV - OAEP isn't IV-based.</summary>
    public string WrappedUvk { get; set; } = string.Empty;

    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Soft-revoke marker; null means the grant is active. Preserved (not deleted) as an audit trail of who had access when.</summary>
    public DateTimeOffset? RevokedAt { get; set; }
}
