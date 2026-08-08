namespace AuthSystem.Domain.Entities;

/// <summary>
/// One row per user who has set up their encrypted personal-info vault. All crypto material here
/// is meaningless to the server on its own - see AuthSystem.Api's CLAUDE.md / the vault feature
/// plan for the zero-knowledge key hierarchy this backs.
/// </summary>
public class VaultProfile
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    /// <summary>Base64 SPKI DER export of the user's RSA-OAEP public key.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Base64, random 16 bytes. Combined with the user's password (client-side, via PBKDF2) to derive their Master Key.</summary>
    public string KdfSalt { get; set; } = string.Empty;

    /// <summary>PBKDF2 iteration count used for this user. Stored per-user so raising the app-wide default later doesn't strand existing vaults.</summary>
    public int KdfIterations { get; set; }

    /// <summary>base64(12-byte IV || AES-GCM ciphertext+tag) of the PKCS8 private key, wrapped under the password-derived Master Key.</summary>
    public string PrivateKeyWrappedByMasterKey { get; set; } = string.Empty;

    /// <summary>Same shape, wrapped under the one-time recovery key shown to the user at setup and never stored anywhere.</summary>
    public string PrivateKeyWrappedByRecoveryKey { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
