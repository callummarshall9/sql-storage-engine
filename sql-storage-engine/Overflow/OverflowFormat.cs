using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Overflow;

public readonly record struct OverflowReference(PageId FirstPageId, long TotalLength);

public readonly record struct OverflowPageHeader(PageId? NextPageId, uint UsedLength);

/// <summary>Encodes fixed-width references from rows to exclusively owned overflow chains.</summary>
public static class OverflowReferenceCodec
{
    public const int EncodedLength = 16;
    public const long MaximumValueLength = 64L * 1024 * 1024;
    public const int MaximumChainLength = 8192;

    public static void Write(Span<byte> destination, OverflowReference reference)
    {
        if (destination.Length < EncodedLength) throw new ArgumentException("Overflow reference destination is truncated.", nameof(destination));
        Validate(reference);
        BinaryPrimitives.WriteUInt64LittleEndian(destination, reference.FirstPageId.Value);
        BinaryPrimitives.WriteInt64LittleEndian(destination[8..], reference.TotalLength);
    }

    public static OverflowReference Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < EncodedLength) throw new StorageFormatException("Overflow reference is truncated.");
        var reference = new OverflowReference(new PageId(BinaryPrimitives.ReadUInt64LittleEndian(source)),
            BinaryPrimitives.ReadInt64LittleEndian(source[8..]));
        Validate(reference);
        return reference;
    }

    public static void Validate(OverflowReference reference)
    {
        if (reference.FirstPageId.Value == 0) throw new StorageFormatException("Overflow chains cannot begin at page zero.");
        if (reference.TotalLength <= 0 || reference.TotalLength > MaximumValueLength)
            throw new StorageFormatException($"Overflow length must be between 1 and {MaximumValueLength} bytes.");
    }
}

/// <summary>Encodes and validates one complete overflow page.</summary>
public static class OverflowPageCodec
{
    public const int HeaderLength = 48;
    public const int NextPageOffset = 32;
    public const int UsedLengthOffset = 41;

    public static int GetPayloadCapacity(int pageSize)
    {
        if (!PageConstants.IsSupportedSize(pageSize)) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return pageSize - HeaderLength;
    }

    public static void Initialize(Span<byte> page, PageId pageId, PageId? nextPageId, ReadOnlySpan<byte> payload)
    {
        var capacity = GetPayloadCapacity(page.Length);
        if (pageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(pageId));
        if (nextPageId?.Value == 0 || nextPageId == pageId) throw new ArgumentException("Invalid overflow next-page ID.", nameof(nextPageId));
        if (payload.IsEmpty || payload.Length > capacity) throw new ArgumentException("Overflow payload length is invalid.", nameof(payload));
        page.Clear();
        PageHeaderCodec.Write(page, new PageHeader(pageId, PageType.Overflow, PageFormatVersion.Current,
            new LogSequenceNumber(0), PageChecksumAlgorithm.Crc32, 0));
        page[NextPageOffset] = nextPageId.HasValue ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt64LittleEndian(page[(NextPageOffset + 1)..], nextPageId.GetValueOrDefault().Value);
        BinaryPrimitives.WriteUInt32LittleEndian(page[UsedLengthOffset..], checked((uint)payload.Length));
        payload.CopyTo(page[HeaderLength..]);
        PageChecksum.WriteChecksum(page, page.Length);
    }

    public static OverflowPageHeader ReadHeader(ReadOnlySpan<byte> page, PageId expectedPageId)
    {
        var capacity = GetPayloadCapacity(page.Length);
        PageChecksum.ValidateChecksum(page, page.Length);
        PageHeaderCodec.Read(page).Validate(expectedPageId, PageType.Overflow);
        if (ContainsNonZero(page[45..HeaderLength])) throw new StorageFormatException("Reserved overflow-header bytes must be zero.");
        PageId? next = page[NextPageOffset] switch
        {
            0 when BinaryPrimitives.ReadUInt64LittleEndian(page[(NextPageOffset + 1)..]) == 0 => null,
            1 => new PageId(BinaryPrimitives.ReadUInt64LittleEndian(page[(NextPageOffset + 1)..])),
            _ => throw new StorageFormatException("Invalid nullable overflow next-page encoding.")
        };
        if (next?.Value == 0 || next == expectedPageId) throw new StorageCorruptionException("Overflow page has an invalid next-page ID.");
        var usedLength = BinaryPrimitives.ReadUInt32LittleEndian(page[UsedLengthOffset..]);
        if (usedLength == 0 || usedLength > capacity) throw new StorageCorruptionException("Overflow used length is outside the payload region.");
        return new OverflowPageHeader(next, usedLength);
    }

    public static ReadOnlyMemory<byte> ReadPayload(ReadOnlySpan<byte> page, PageId expectedPageId)
    {
        var header = ReadHeader(page, expectedPageId);
        return page.Slice(HeaderLength, checked((int)header.UsedLength)).ToArray();
    }

    private static bool ContainsNonZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes) if (value != 0) return true;
        return false;
    }
}
