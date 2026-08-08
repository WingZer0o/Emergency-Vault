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

    /// <summary>base64(12-byte IV || AES-GCM ciphertext+tag) of the item's JSON payload, encrypted with the owner's User Vault Key.</summary>
    public string CiphertextBlob { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
