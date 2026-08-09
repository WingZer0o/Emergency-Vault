using System.Data;
using AuthSystem.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AuthSystem.Infrastructure.Persistence;

/// <summary>
/// Streams VaultItemAttachment.CiphertextBytes directly to/from SQL Server via raw ADO.NET,
/// since EF Core has no first-class streaming blob API. Uses its own SqlConnection (independent of
/// AppDbContext's) so it doesn't interfere with EF's own connection lifecycle/pooling assumptions.
/// </summary>
public class SqlVaultAttachmentBlobStore(AppDbContext db) : IVaultAttachmentBlobStore
{
    public async Task WriteAsync(Guid attachmentId, Stream content, CancellationToken ct)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 0; // large uploads can legitimately take longer than the 30s default
        command.CommandText = "UPDATE VaultItemAttachments SET CiphertextBytes = @bytes WHERE Id = @id";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = attachmentId });
        // SqlClient streams a Stream-valued varbinary(max) parameter in chunks rather than
        // materializing it - this is what keeps the upload path from buffering the whole file.
        command.Parameters.Add(new SqlParameter("@bytes", SqlDbType.VarBinary, -1) { Value = content });

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<Stream> OpenReadAsync(Guid attachmentId, CancellationToken ct)
    {
        var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync(ct);

        var command = connection.CreateCommand();
        command.CommandTimeout = 0;
        command.CommandText = "SELECT CiphertextBytes FROM VaultItemAttachments WHERE Id = @id";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.UniqueIdentifier) { Value = attachmentId });

        // SequentialAccess is required for SqlDataReader.GetStream to actually stream from the wire
        // rather than buffering the column value first.
        var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleRow, ct);
        if (!await reader.ReadAsync(ct))
        {
            await reader.DisposeAsync();
            await connection.DisposeAsync();
            throw new InvalidOperationException($"Attachment {attachmentId} has no stored blob.");
        }

        var blobStream = reader.GetStream(0);
        return new OwnedBlobStream(blobStream, reader, connection);
    }

    private string ConnectionString() =>
        db.Database.GetConnectionString() ?? throw new InvalidOperationException("No connection string configured for AppDbContext.");

    /// <summary>Wraps the reader's blob stream so disposing it also releases the SqlDataReader and
    /// SqlConnection this streaming read opened - lets callers (e.g. FileStreamResult) dispose a
    /// single object once they're done copying it to the response.</summary>
    private sealed class OwnedBlobStream(Stream inner, SqlDataReader reader, SqlConnection connection) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            inner.ReadAsync(buffer, offset, count, ct);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            inner.ReadAsync(buffer, ct);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                reader.Dispose();
                connection.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            await reader.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
