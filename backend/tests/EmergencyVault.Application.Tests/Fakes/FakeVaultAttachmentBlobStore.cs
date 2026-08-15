using EmergencyVault.Application.Interfaces;

namespace EmergencyVault.Application.Tests.Fakes;

/// <summary>In-memory stand-in for the SQL-streamed attachment blob store.</summary>
public class FakeVaultAttachmentBlobStore : IVaultAttachmentBlobStore
{
    private readonly Dictionary<Guid, byte[]> _blobs = [];

    public async Task WriteAsync(Guid attachmentId, Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _blobs[attachmentId] = buffer.ToArray();
    }

    public Task<Stream> OpenReadAsync(Guid attachmentId, CancellationToken ct) =>
        Task.FromResult<Stream>(new MemoryStream(_blobs[attachmentId]));
}
