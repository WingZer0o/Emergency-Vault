namespace EmergencyVault.Domain.Entities;

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

    /// <summary>Null = whole-vault share (the original, and only, kind - including the self-share
    /// row). Set = scoped to exactly one item, granted independently of any whole-vault share.</summary>
    public Guid? VaultItemId { get; set; }
    public VaultItem? VaultItem { get; set; }

    /// <summary>Only meaningful when VaultItemId is set - a whole-vault holder already has
    /// owner-equivalent read access via the UVK, and there is no whole-vault write capability.</summary>
    public bool CanEdit { get; set; }

    /// <summary>base64 ciphertext of the raw key this share grants, wrapped under the holder's
    /// public key. Meaning depends on VaultItemId: null means this is the owner's whole UVK
    /// (unchanged from the original design); set means this is that one item's own key (IK),
    /// wrapped DIRECTLY for the holder - never via any UVK, so an item-scoped holder can never
    /// touch any other item, even in principle.</summary>
    public string WrappedKey { get; set; } = string.Empty;

    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Soft-revoke marker; null means the grant is active. Preserved (not deleted) as an audit trail of who had access when.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Email verification gate on top of RevokedAt: null means the holder hasn't entered the emailed
    /// code yet, so WrappedUvk must not be handed out and shared items must not be readable. Always
    /// set at creation time for the self-share row - only cross-user shares go through the code flow.
    /// </summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    /// <summary>
    /// base64 20-byte HOTP secret for this share's verification code. Kept (not nulled) after the
    /// initial accept - it's reused to generate the per-session access codes required every time
    /// the holder opens this shared vault. Null for shares created before that reuse existed;
    /// those get a fresh secret lazily generated on first post-acceptance access request.
    /// </summary>
    public string? VerificationSecret { get; set; }

    /// <summary>HOTP counter, bumped every time a code is issued or resent. Never reused once a code has gone out.</summary>
    public long VerificationCounter { get; set; }

    /// <summary>Wrong-code attempts against the current code. Hitting MaxAttempts kills the share permanently - no further resend.</summary>
    public int VerificationAttempts { get; set; }

    /// <summary>When the current code goes stale; a resend issues a new one and extends this.</summary>
    public DateTimeOffset? VerificationCodeExpiresAt { get; set; }

    /// <summary>Anchor for the resend cooldown.</summary>
    public DateTimeOffset? VerificationCodeSentAt { get; set; }
}
