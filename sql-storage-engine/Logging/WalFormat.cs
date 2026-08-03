using System.Buffers.Binary;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public enum WalRecordType : ushort { Begin = 1, PageChange = 2, Commit = 3, Rollback = 4, Checkpoint = 5, Compensation = 6 }
public sealed record WalSegmentHeader(DatabaseId DatabaseId, ulong Timeline, ulong SegmentNumber);
public sealed record WalRecord(LogSequenceNumber Lsn, LogSequenceNumber PreviousLsn,
    TransactionId TransactionId, WalRecordType Type, ReadOnlyMemory<byte> Payload);
public sealed record WalReadResult(IReadOnlyList<WalRecord> Records, bool HasIncompleteTail, int ValidLength);

/// <summary>Encodes version-one WAL segment and checksummed record envelopes using explicit endian rules.</summary>
public static class WalFormat
{
    public const ushort Version = 1;
    public const int SegmentHeaderLength = 48;
    public const int RecordHeaderLength = 40;
    public const int MaximumPayloadLength = 16 * 1024 * 1024;
    private const uint SegmentMagic = 0x314C4157; // WAL1

    public static byte[] WriteSegmentHeader(WalSegmentHeader header)
    {
        if (header.Timeline == 0) throw new ArgumentOutOfRangeException(nameof(header));
        var bytes = new byte[SegmentHeaderLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, SegmentMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), Version);
        header.DatabaseId.Value.TryWriteBytes(bytes.AsSpan(8, 16), bigEndian: true, out _);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), header.Timeline);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(32), header.SegmentNumber);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), Crc32(bytes.AsSpan(0, 44)));
        return bytes;
    }

    public static WalSegmentHeader ReadSegmentHeader(ReadOnlySpan<byte> source)
    {
        if (source.Length < SegmentHeaderLength) throw new StorageFormatException("WAL segment header is truncated.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(source) != SegmentMagic) throw new StorageFormatException("Invalid WAL segment magic.");
        var version = BinaryPrimitives.ReadUInt16LittleEndian(source[4..]);
        if (version != Version) throw new StorageFormatException($"Unsupported WAL version {version}.");
        if (source.Slice(6, 2).IndexOfAnyExcept((byte)0) >= 0 || source.Slice(40, 4).IndexOfAnyExcept((byte)0) >= 0)
            throw new StorageFormatException("Reserved WAL segment bytes must be zero.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(source[44..]) != Crc32(source[..44]))
            throw new StorageCorruptionException("WAL segment-header checksum mismatch.");
        var timeline = BinaryPrimitives.ReadUInt64LittleEndian(source[24..]);
        if (timeline == 0) throw new StorageFormatException("WAL timeline must be nonzero.");
        return new WalSegmentHeader(new DatabaseId(new Guid(source.Slice(8, 16), bigEndian: true)), timeline,
            BinaryPrimitives.ReadUInt64LittleEndian(source[32..]));
    }

    public static byte[] WriteRecord(WalRecord record)
    {
        if (record.Lsn.Value == 0 || record.TransactionId.Value == 0) throw new ArgumentOutOfRangeException(nameof(record));
        if (!Enum.IsDefined(record.Type)) throw new ArgumentOutOfRangeException(nameof(record));
        if (record.Payload.Length > MaximumPayloadLength) throw new ArgumentException("WAL payload exceeds its bound.", nameof(record));
        var bytes = new byte[checked(RecordHeaderLength + record.Payload.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, checked((uint)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), Version);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), (ushort)record.Type);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8), record.Lsn.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16), record.PreviousLsn.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(24), record.TransactionId.Value);
        record.Payload.Span.CopyTo(bytes.AsSpan(RecordHeaderLength));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(36), Crc32WithZero(bytes, 36));
        return bytes;
    }

    public static WalRecord ReadRecord(ReadOnlySpan<byte> source)
    {
        if (source.Length < RecordHeaderLength) throw new StorageFormatException("WAL record is truncated.");
        var length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source));
        if (length != source.Length || length > RecordHeaderLength + MaximumPayloadLength) throw new StorageFormatException("Invalid WAL record length.");
        var version = BinaryPrimitives.ReadUInt16LittleEndian(source[4..]);
        if (version != Version) throw new StorageFormatException($"Unsupported WAL record version {version}.");
        var type = (WalRecordType)BinaryPrimitives.ReadUInt16LittleEndian(source[6..]);
        if (!Enum.IsDefined(type)) throw new StorageFormatException($"Unknown WAL record type {(ushort)type}.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(source[32..]) != 0) throw new StorageFormatException("Reserved WAL record bytes must be zero.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(source[36..]) != Crc32WithZero(source, 36))
            throw new StorageCorruptionException("WAL record checksum mismatch.");
        return new WalRecord(new LogSequenceNumber(BinaryPrimitives.ReadUInt64LittleEndian(source[8..])),
            new LogSequenceNumber(BinaryPrimitives.ReadUInt64LittleEndian(source[16..])),
            new TransactionId(BinaryPrimitives.ReadUInt64LittleEndian(source[24..])), type,
            source[RecordHeaderLength..].ToArray());
    }

    public static WalReadResult ReadRecords(ReadOnlySpan<byte> source)
    {
        List<WalRecord> records = [];
        var offset = 0;
        while (offset < source.Length)
        {
            if (source.Length - offset < sizeof(uint)) return new WalReadResult(records, true, offset);
            var length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]));
            if (length < RecordHeaderLength || length > RecordHeaderLength + MaximumPayloadLength)
                throw new StorageCorruptionException("Invalid WAL record length within the log.");
            if (length > source.Length - offset) return new WalReadResult(records, true, offset);
            records.Add(ReadRecord(source.Slice(offset, length)));
            offset = checked(offset + length);
        }
        return new WalReadResult(records, false, offset);
    }

    private static uint Crc32WithZero(ReadOnlySpan<byte> bytes, int zeroOffset)
    { var copy = bytes.ToArray(); copy.AsSpan(zeroOffset, 4).Clear(); return Crc32(copy); }
    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xffffffffu;
        foreach (var value in bytes) { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1)); }
        return ~crc;
    }
}
