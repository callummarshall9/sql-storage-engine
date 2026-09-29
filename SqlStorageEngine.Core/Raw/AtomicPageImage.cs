using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Transactions;

namespace SqlStorageEngine.Core;

/// <summary>Bounded checksummed page image, published by durable same-directory replacement.</summary>
internal sealed class AtomicPageImage : IAsyncDisposable
{
    private const int Size = PageConstants.DefaultSize;
    private const int Prefix = PageHeaderCodec.EncodedLength + 8;
    private readonly string path;
    private readonly FileStream lease;

    private AtomicPageImage(string path, FileStream lease) { this.path = path; this.lease = lease; }

    internal static AtomicPageImage Acquire(string path, bool creating)
    {
        var full = Path.GetFullPath(path);
        if (creating) Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        // Keep the lock inode across close/reopen, including replacement of the data inode.
        var lease = new FileStream(full + ".storage-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try
        {
            if (creating == File.Exists(full)) throw new IOException("Unexpected store existence.");
            // A killed writer can leave only this bounded, uncommitted staging image.
            var pending = full + ".pending";
            if (File.Exists(pending)) TransactionDirectory.Delete(pending);
            return new(full, lease);
        }
        catch { lease.Dispose(); throw; }
    }

    internal async ValueTask<byte[]> ReadAsync(CancellationToken token)
    {
        var length = new FileInfo(path).Length;
        var maximumPages = (PageStateCodec.MaximumBytes + Size - Prefix - 1) / (Size - Prefix);
        if (length <= 0 || length % Size != 0 || length / Size > maximumPages)
            throw new InvalidDataException("Invalid page image length.");
        await using var store = FilePageStore.OpenExisting(path, Size, readOnly: true);
        byte[]? payload = null;
        var offset = 0;
        for (ulong i = 0; i < (ulong)(length / Size); i++)
        {
            var page = new byte[Size];
            await store.ReadAsync(new(i), page, token).ConfigureAwait(false);
            PageChecksum.ValidateChecksum(page, Size);
            PageHeaderCodec.Read(page).Validate(new(i), PageType.Overflow);
            if (!page.AsSpan(PageHeaderCodec.EncodedLength, 4).SequenceEqual("PGC1"u8))
                throw new InvalidDataException("Unsupported page image format.");
            var total = BinaryPrimitives.ReadInt32LittleEndian(page.AsSpan(Prefix - 4));
            if (total is < 60 or > PageStateCodec.MaximumBytes ||
                (total + Size - Prefix - 1) / (Size - Prefix) != length / Size)
                throw new InvalidDataException("Invalid page image payload length.");
            payload ??= new byte[total];
            if (payload.Length != total) throw new InvalidDataException("Inconsistent page image length.");
            var count = Math.Min(Size - Prefix, total - offset);
            page.AsSpan(Prefix, count).CopyTo(payload.AsSpan(offset));
            if (page.AsSpan(Prefix + count).IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException("Invalid page image padding.");
            offset += count;
        }
        return payload!;
    }

    internal async ValueTask PublishAsync(byte[] payload, CancellationToken token, string? phase,
        Func<string, ValueTask>? checkpoint)
    {
        var pending = path + ".pending";
        try
        {
            await using (var store = FilePageStore.CreateNew(pending, Size))
            {
                ulong id = 0;
                for (var offset = 0; offset < payload.Length; offset += Size - Prefix)
                {
                    var page = new byte[Size];
                    PageHeaderCodec.Write(page, new(new(id), PageType.Overflow, PageFormatVersion.Current,
                        new LogSequenceNumber(0), PageChecksumAlgorithm.Crc32, 0));
                    "PGC1"u8.CopyTo(page.AsSpan(PageHeaderCodec.EncodedLength));
                    BinaryPrimitives.WriteInt32LittleEndian(page.AsSpan(Prefix - 4), payload.Length);
                    payload.AsSpan(offset, Math.Min(Size - Prefix, payload.Length - offset)).CopyTo(page.AsSpan(Prefix));
                    PageChecksum.WriteChecksum(page, Size);
                    await store.WriteAsync(new(id++), page, token).ConfigureAwait(false);
                }
                await store.FlushAsync(token).ConfigureAwait(false);
            }
            if (phase is not null && checkpoint is not null) await checkpoint(phase + "-written").ConfigureAwait(false);
            File.Move(pending, path, overwrite: true);
            TransactionDirectory.Flush(path);
            if (phase is not null && checkpoint is not null) await checkpoint(phase + "-committed").ConfigureAwait(false);
        }
        finally { if (File.Exists(pending)) TransactionDirectory.Delete(pending); }
    }

    public ValueTask DisposeAsync() => lease.DisposeAsync();
}
