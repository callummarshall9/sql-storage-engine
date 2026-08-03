using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public sealed record PhysicalPageChange(PageId PageId, PageType PageType,
    ReadOnlyMemory<byte> BeforeImage, ReadOnlyMemory<byte> AfterImage);

/// <summary>Encodes bounded full-page images used for idempotent redo and undo.</summary>
public static class PhysicalPageChangeCodec
{
    public const int HeaderLength = 24;
    public static byte[] Write(PhysicalPageChange change)
    {
        if (change.BeforeImage.Length != change.AfterImage.Length || !PageConstants.IsSupportedSize(change.AfterImage.Length))
            throw new ArgumentException("Physical images must have one equal supported page size.", nameof(change));
        var bytes = new byte[checked(HeaderLength + change.BeforeImage.Length + change.AfterImage.Length)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, change.PageId.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)change.PageType);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), checked((uint)change.BeforeImage.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), checked((uint)change.AfterImage.Length));
        change.BeforeImage.Span.CopyTo(bytes.AsSpan(HeaderLength));
        change.AfterImage.Span.CopyTo(bytes.AsSpan(HeaderLength + change.BeforeImage.Length));
        return bytes;
    }

    public static PhysicalPageChange Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < HeaderLength) throw new StorageFormatException("Physical page-change header is truncated.");
        if (source.Slice(10, 2).IndexOfAnyExcept((byte)0) >= 0 || source.Slice(20, 4).IndexOfAnyExcept((byte)0) >= 0)
            throw new StorageFormatException("Reserved physical-change bytes must be zero.");
        var before = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source[12..]));
        var after = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source[16..]));
        if (before != after || !PageConstants.IsSupportedSize(before) || source.Length != HeaderLength + before + after)
            throw new StorageFormatException("Physical page-change image lengths are invalid.");
        var type = (PageType)BinaryPrimitives.ReadUInt16LittleEndian(source[8..]);
        if (!Enum.IsDefined(type) || type == PageType.Unknown) throw new StorageFormatException("Invalid physical page type.");
        return new PhysicalPageChange(new PageId(BinaryPrimitives.ReadUInt64LittleEndian(source)), type,
            source.Slice(HeaderLength, before).ToArray(), source.Slice(HeaderLength + before, after).ToArray());
    }
}

/// <summary>Replays committed full-page changes when their LSN is newer than the stored page.</summary>
public static class RecoveryRedo
{
    public static async ValueTask ApplyAsync(IPageStore pageStore, RecoveryAnalysis analysis,
        CancellationToken cancellationToken = default)
    {
        foreach (var record in analysis.Records.Where(record => record.Type == WalRecordType.PageChange &&
                     analysis.Transactions.GetValueOrDefault(record.TransactionId) == Transactions.TransactionState.Committed))
        {
            var change = PhysicalPageChangeCodec.Read(record.Payload.Span);
            var after = change.AfterImage.ToArray();
            ValidateImage(after, change, record.Lsn);
            var current = new byte[pageStore.PageSize];
            await pageStore.ReadAsync(change.PageId, current, cancellationToken).ConfigureAwait(false);
            try
            {
                PageChecksum.ValidateChecksum(current, pageStore.PageSize);
                var header = PageHeaderCodec.Read(current);
                header.Validate(change.PageId, change.PageType);
                if (header.PageLogSequenceNumber.Value >= record.Lsn.Value) continue;
            }
            catch (StorageCorruptionException)
            {
                // A verified logged full-page image is the only source allowed to repair a torn/corrupt page.
            }
            await pageStore.WriteAsync(change.PageId, after, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ValidateImage(byte[] image, PhysicalPageChange change, LogSequenceNumber expectedLsn)
    {
        PageChecksum.ValidateChecksum(image, image.Length);
        var header = PageHeaderCodec.Read(image);
        header.Validate(change.PageId, change.PageType);
        if (header.PageLogSequenceNumber != expectedLsn)
            throw new StorageCorruptionException("Logged after-image LSN does not match its WAL record.");
    }
}
