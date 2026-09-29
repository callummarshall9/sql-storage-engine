using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>Encodes the 32-byte common page header in little-endian field order.</summary>
public static class PageHeaderCodec
{
    public const int EncodedLength = 32;
    public const int ChecksumOffset = 28;

    public static void Write(Span<byte> destination, PageHeader header)
    {
        if (destination.Length < EncodedLength)
            throw new ArgumentException($"Destination must contain at least {EncodedLength} bytes.", nameof(destination));
        header.Validate(header.PageId);
        destination[..EncodedLength].Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(destination, header.PageId.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], (ushort)header.PageType);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[10..], header.FormatVersion.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[12..], header.PageLogSequenceNumber.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[20..], (ushort)header.ChecksumAlgorithm);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[ChecksumOffset..], header.Checksum);
    }

    public static PageHeader Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < EncodedLength)
            throw new StorageFormatException($"Page header is truncated; expected {EncodedLength} bytes.");
        if (!source[22..ChecksumOffset].IsEmpty && ContainsNonZero(source[22..ChecksumOffset]))
            throw new StorageFormatException("Reserved page-header bytes must be zero.");
        var header = new PageHeader(
            new PageId(BinaryPrimitives.ReadUInt64LittleEndian(source)),
            (PageType)BinaryPrimitives.ReadUInt16LittleEndian(source[8..]),
            new PageFormatVersion(BinaryPrimitives.ReadUInt16LittleEndian(source[10..])),
            new LogSequenceNumber(BinaryPrimitives.ReadUInt64LittleEndian(source[12..])),
            (PageChecksumAlgorithm)BinaryPrimitives.ReadUInt16LittleEndian(source[20..]),
            BinaryPrimitives.ReadUInt32LittleEndian(source[ChecksumOffset..]));
        header.Validate(header.PageId);
        return header;
    }

    private static bool ContainsNonZero(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
            if (value != 0) return true;
        return false;
    }
}
