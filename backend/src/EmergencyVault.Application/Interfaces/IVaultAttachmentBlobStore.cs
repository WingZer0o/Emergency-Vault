namespace EmergencyVault.Application.Interfaces;

/// <summary>
/// Streams the CiphertextBytes column of a VaultItemAttachment directly to/from SQL Server,
/// bypassing EF Core (which has no first-class streaming blob support) so large files never get
/// buffered whole in server process memory. The attachment's metadata row must already exist
/// (via IVaultRepository) before WriteAsync is called.
/// </summary>
public interface IVaultAttachmentBlobStore
{
    Task WriteAsync(Guid attachmentId, Stream content, CancellationToken ct);

    /// <summary>Opens a readable stream over the stored ciphertext for the caller to copy directly
    /// to the HTTP response. Disposing the returned stream releases the underlying reader/connection.</summary>
    Task<Stream> OpenReadAsync(Guid attachmentId, CancellationToken ct);
}
