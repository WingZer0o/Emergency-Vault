namespace EmergencyVault.Domain.Entities;

/// <summary>
/// Snapshot of a vault item's ciphertext from just before an edit overwrote it. Append-only audit
/// log - never updated, only inserted (and cascade-deleted with the item). PreviousCiphertextBlob
/// remains decryptable with whatever key decrypts the item's current CiphertextBlob (the item key
/// never changes once assigned - see VaultItem.WrappedItemKey), so no key material is duplicated here.
/// </summary>
public class VaultItemRevision
{
    public Guid Id { get; set; }

    public Guid VaultItemId { get; set; }
    public VaultItem VaultItem { get; set; } = null!;

    /// <summary>base64(12-byte IV || AES-GCM ciphertext+tag) of the item's JSON payload as it was
    /// immediately before this edit - same encoding/key rules as VaultItem.CiphertextBlob.</summary>
    public string PreviousCiphertextBlob { get; set; } = string.Empty;

    public Guid ChangedByUserId { get; set; }
    public ApplicationUser ChangedBy { get; set; } = null!;

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
