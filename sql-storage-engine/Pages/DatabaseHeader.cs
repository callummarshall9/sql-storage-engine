using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>Bootstrap metadata stored in page zero.</summary>
public sealed record DatabaseHeader(
    DatabaseId DatabaseId,
    int PageSize,
    ushort FormatVersion,
    PageId? CatalogRootPageId,
    PageId? FreeListHeadPageId,
    TableId NextTableId,
    IndexId NextIndexId,
    TransactionId NextTransactionId,
    PageId NextPageId,
    bool IsCleanShutdown)
{
    public const ushort CurrentFormatVersion = 1;
}

/// <summary>Encodes and validates the fixed page-zero database header.</summary>
public static class DatabaseHeaderCodec
{
    public const int PayloadOffset = PageHeaderCodec.EncodedLength;
    public const int EncodedMetadataLength = 82;
    private static ReadOnlySpan<byte> Magic => "SQLSTORE"u8;

    public static void Write(Span<byte> page, DatabaseHeader header)
    {
        ValidateBufferAndHeader(page, header);
        page.Clear();
        var common = new PageHeader(new PageId(0), PageType.DatabaseHeader, PageFormatVersion.Current,
            new LogSequenceNumber(0), PageChecksumAlgorithm.Crc32, 0);
        PageHeaderCodec.Write(page, common);
        var data = page[PayloadOffset..];
        Magic.CopyTo(data);
        WriteCanonicalGuid(data[8..24], header.DatabaseId.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(data[24..], header.FormatVersion);
        data[26] = header.IsCleanShutdown ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32LittleEndian(data[28..], header.PageSize);
        WriteOptionalPage(data[32..], header.CatalogRootPageId);
        WriteOptionalPage(data[41..], header.FreeListHeadPageId);
        BinaryPrimitives.WriteUInt64LittleEndian(data[50..], header.NextTableId.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(data[58..], header.NextIndexId.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(data[66..], header.NextTransactionId.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(data[74..], header.NextPageId.Value);
        PageChecksum.WriteChecksum(page, header.PageSize);
    }

    public static DatabaseHeader Read(ReadOnlySpan<byte> page)
    {
        if (page.Length < PageConstants.MinimumSize)
            throw new StorageFormatException("Database header page is truncated.");
        var data = page[PayloadOffset..];
        if (!data[..8].SequenceEqual(Magic)) throw new InvalidDatabaseMagicException();
        var pageSize = BinaryPrimitives.ReadInt32LittleEndian(data[28..]);
        if (!PageConstants.IsSupportedSize(pageSize) || page.Length != pageSize)
            throw new InvalidPageSizeException(pageSize);
        var version = BinaryPrimitives.ReadUInt16LittleEndian(data[24..]);
        if (version != DatabaseHeader.CurrentFormatVersion)
            throw new UnsupportedDatabaseVersionException(version);
        if (data[26] > 1 || data[27] != 0)
            throw new StorageFormatException("Invalid shutdown marker or reserved database-header byte.");
        PageChecksum.ValidateChecksum(page, pageSize);
        var common = PageHeaderCodec.Read(page);
        common.Validate(new PageId(0), PageType.DatabaseHeader);
        return new DatabaseHeader(
            new DatabaseId(ReadCanonicalGuid(data[8..24])), pageSize, version,
            ReadOptionalPage(data[32..]), ReadOptionalPage(data[41..]),
            new TableId(BinaryPrimitives.ReadUInt64LittleEndian(data[50..])),
            new IndexId(BinaryPrimitives.ReadUInt64LittleEndian(data[58..])),
            new TransactionId(BinaryPrimitives.ReadUInt64LittleEndian(data[66..])),
            new PageId(BinaryPrimitives.ReadUInt64LittleEndian(data[74..])), data[26] == 1);
    }

    private static void ValidateBufferAndHeader(Span<byte> page, DatabaseHeader header)
    {
        if (page.Length != header.PageSize) throw new ArgumentException("Buffer length must equal the database page size.", nameof(page));
        if (!PageConstants.IsSupportedSize(header.PageSize)) throw new InvalidPageSizeException(header.PageSize);
        if (header.FormatVersion != DatabaseHeader.CurrentFormatVersion) throw new UnsupportedDatabaseVersionException(header.FormatVersion);
        if (header.NextPageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(header), "Next page ID must follow page zero.");
    }

    private static void WriteOptionalPage(Span<byte> destination, PageId? value)
    {
        destination[0] = value.HasValue ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteUInt64LittleEndian(destination[1..], value.GetValueOrDefault().Value);
    }

    private static PageId? ReadOptionalPage(ReadOnlySpan<byte> source) => source[0] switch
    {
        0 when BinaryPrimitives.ReadUInt64LittleEndian(source[1..]) == 0 => null,
        1 => new PageId(BinaryPrimitives.ReadUInt64LittleEndian(source[1..])),
        _ => throw new StorageFormatException("Invalid nullable page identifier encoding.")
    };

    // RFC 4122 byte order avoids Guid.ToByteArray's mixed-endian historical layout.
    private static void WriteCanonicalGuid(Span<byte> destination, Guid value)
    {
        Span<byte> mixed = stackalloc byte[16];
        value.TryWriteBytes(mixed);
        destination[0] = mixed[3]; destination[1] = mixed[2]; destination[2] = mixed[1]; destination[3] = mixed[0];
        destination[4] = mixed[5]; destination[5] = mixed[4]; destination[6] = mixed[7]; destination[7] = mixed[6];
        mixed[8..].CopyTo(destination[8..]);
    }

    private static Guid ReadCanonicalGuid(ReadOnlySpan<byte> source)
    {
        Span<byte> mixed = stackalloc byte[16];
        mixed[0] = source[3]; mixed[1] = source[2]; mixed[2] = source[1]; mixed[3] = source[0];
        mixed[4] = source[5]; mixed[5] = source[4]; mixed[6] = source[7]; mixed[7] = source[6];
        source[8..16].CopyTo(mixed[8..]);
        return new Guid(mixed);
    }
}
