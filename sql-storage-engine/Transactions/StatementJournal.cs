using System.Buffers.Binary;
using System.Security.Cryptography;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Transactions;

/// <summary>A database-wide before-image used to make a high-level mutation statement crash atomic.</summary>
internal sealed class StatementJournal : IAsyncDisposable
{
    private static ReadOnlySpan<byte> Magic => "SQLSTMT1"u8;
    private const int HeaderLength = 64;
    private const int StateOffset = 8;
    private const byte Active = 1;
    private const byte Committed = 2;
    private readonly string _path;
    private bool _completed;

    private StatementJournal(string path) => _path = path;

    public static string GetPath(string databasePath) => databasePath + ".statement-undo";

    public static async ValueTask<StatementJournal> CreateAsync(string databasePath,
        CancellationToken cancellationToken, string? explicitPath = null)
    {
        var journalPath = explicitPath ?? GetPath(databasePath);
        if (File.Exists(journalPath))
            throw new StorageResourceException("A statement undo journal already exists.", new IOException());
        var temporaryPath = journalPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using var source = new FileStream(databasePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var header = new byte[HeaderLength];
            Magic.CopyTo(header);
            header[StateOffset] = Active;
            BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(16), source.Length);
            await destination.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 1024];
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
            {
                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            hash.GetHashAndReset().CopyTo(header, 24);
            destination.Position = 0;
            await destination.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            destination.Flush(flushToDisk: true);
            File.Move(temporaryPath, journalPath, overwrite: false);
            TransactionDirectory.Flush(journalPath);
            return new StatementJournal(journalPath);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    public async ValueTask MarkCommittedAsync(CancellationToken cancellationToken, bool retain = false)
    {
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.None,
            1, FileOptions.Asynchronous | FileOptions.RandomAccess);
        stream.Position = StateOffset;
        await stream.WriteAsync(new byte[] { Committed }, cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
        _completed = !retain;
        if (!retain) TransactionDirectory.Delete(_path);
    }

    public async ValueTask RestoreAsync(PageDatabase database, CancellationToken cancellationToken, bool retain = false)
    {
        var snapshot = await ReadSnapshotAsync(_path, cancellationToken).ConfigureAwait(false);
        if (snapshot.State != Active) throw new StorageFormatException("Only an active statement journal can be restored.");
        if (snapshot.Bytes.LongLength % database.PageSize != 0)
            throw new StorageFormatException("Statement journal is not page aligned.");
        for (var offset = 0; offset < snapshot.Bytes.Length; offset += database.PageSize)
            await database.WriteAsync(new PageId(checked((ulong)(offset / database.PageSize))),
                snapshot.Bytes.AsMemory(offset, database.PageSize), cancellationToken).ConfigureAwait(false);
        database.RestoreLength(snapshot.Bytes.LongLength);
        await database.FlushAsync(cancellationToken).ConfigureAwait(false);
        await database.ReloadHeaderAsync(cancellationToken).ConfigureAwait(false);
        _completed = !retain;
        if (!retain) TransactionDirectory.Delete(_path);
    }

    public static async ValueTask RecoverIfNeededAsync(string databasePath,
        CancellationToken cancellationToken, string? explicitPath = null, bool retain = false)
    {
        var path = explicitPath ?? GetPath(databasePath);
        if (!File.Exists(path)) return;
        var snapshot = await ReadSnapshotAsync(path, cancellationToken).ConfigureAwait(false);
        if (snapshot.State == Active)
        {
            var temporaryPath = databasePath + $".{Guid.NewGuid():N}.recovery";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, snapshot.Bytes, cancellationToken).ConfigureAwait(false);
                await using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite,
                    FileShare.None, 1, FileOptions.WriteThrough)) stream.Flush(flushToDisk: true);
                File.Move(temporaryPath, databasePath, overwrite: true);
                TransactionDirectory.Flush(databasePath);
            }
            finally { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
        }
        if (!retain) TransactionDirectory.Delete(path);
    }

    internal static async ValueTask<DatabaseId> ReadDatabaseIdentityAsync(string path, CancellationToken token)
    {
        var snapshot = await ReadSnapshotAsync(path, token).ConfigureAwait(false);
        var size = BinaryPrimitives.ReadInt32LittleEndian(snapshot.Bytes.AsSpan(DatabaseHeaderCodec.PayloadOffset + 28));
        if (!PageConstants.IsSupportedSize(size) || snapshot.Bytes.Length < size)
            throw new StorageFormatException("Transaction snapshot header is invalid.");
        return DatabaseHeaderCodec.Read(snapshot.Bytes.AsSpan(0, size)).DatabaseId;
    }

    internal static async ValueTask<bool> IsCommittedAsync(string path, CancellationToken token) =>
        (await ReadSnapshotAsync(path, token).ConfigureAwait(false)).State == Committed;

    internal void Delete() { TransactionDirectory.Delete(_path); _completed = true; }

    public ValueTask DisposeAsync()
    {
        // An incomplete journal is intentionally retained for recovery.
        if (_completed && File.Exists(_path)) TransactionDirectory.Delete(_path);
        return ValueTask.CompletedTask;
    }

    private static async ValueTask<(byte State, byte[] Bytes)> ReadSnapshotAsync(string path,
        CancellationToken cancellationToken)
    {
        var source = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        if (source.Length < HeaderLength || !source.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new StorageFormatException("Statement journal header is invalid.");
        var state = source[StateOffset];
        if (state is not (Active or Committed)) throw new StorageFormatException("Statement journal state is invalid.");
        var length = BinaryPrimitives.ReadInt64LittleEndian(source.AsSpan(16));
        if (length < 0 || length != source.LongLength - HeaderLength || length > int.MaxValue)
            throw new StorageFormatException("Statement journal length is invalid.");
        var bytes = source.AsSpan(HeaderLength).ToArray();
        if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(source.AsSpan(24, 32)))
            throw new StorageCorruptionException("Statement journal checksum is invalid.");
        return (state, bytes);
    }
}
