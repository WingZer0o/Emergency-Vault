namespace EmergencyVault.Domain.Entities;

/// <summary>
/// An encrypted file attached to a VaultItem (DOCX/PDF only). Unlike VaultItem.CiphertextBlob,
/// FileName/ContentType/SizeBytes are deliberately stored in cleartext - a user-approved exception
/// to the vault's zero-knowledge model - so the server can enforce the DOCX/PDF allowlist and set
/// correct download headers. The file's actual bytes (CiphertextBytes) remain fully opaque to the
/// server, encrypted client-side with the owner's User Vault Key.
/// </summary>
public class VaultItemAttachment
{
    public Guid Id { get; set; }

    public Guid VaultItemId { get; set; }
    public VaultItem VaultItem { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Plaintext file size in bytes - cleartext, so listing/progress UI never touches the blob.</summary>
    public long SizeBytes { get; set; }

    /// <summary>base64(12-byte AES-GCM IV) used to encrypt CiphertextBytes.</summary>
    public string Iv { get; set; } = string.Empty;

    /// <summary>AES-GCM ciphertext + 16-byte auth tag. Read/written via streaming ADO.NET
    /// (see IVaultAttachmentBlobStore) - never loaded whole into server process memory.</summary>
    public byte[] CiphertextBytes { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
