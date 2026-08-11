namespace AuthSystem.Domain.Entities;

/// <summary>
/// A single encrypted vault item (e.g. a bank login, an insurance card). The server never sees
/// the plaintext - including the item's own label - so there is deliberately no plaintext
/// label/category column here; that metadata lives inside the encrypted JSON payload instead.
/// </summary>
public class VaultItem
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }
    public ApplicationUser Owner { get; set; } = null!;

    /// <summary>base64(12-byte IV || AES-GCM ciphertext+tag) of the item's JSON payload. Encrypted
    /// with this item's own key once WrappedItemKey is set; for a legacy row where WrappedItemKey
    /// is still null, this remains directly UVK-encrypted, as it was before per-item keys existed.</summary>
    public string CiphertextBlob { get; set; } = string.Empty;

    /// <summary>Null for a legacy item not yet migrated to per-item keys. Once set, base64(IV ||
    /// ciphertext) of this item's own raw AES-256-GCM key, wrapped under the OWNER's User Vault Key
    /// - lets the owner (and any whole-vault share holder, who also holds that UVK) recover it.</summary>
    public string? WrappedItemKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
