using System.Buffers.Binary;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>Calculates and validates IEEE CRC-32 over a complete fixed-size page.</summary>
public static class PageChecksum
{
    public static void WriteChecksum(Span<byte> page, int pageSize)
    {
        ValidateSize(page.Length, pageSize);
        BinaryPrimitives.WriteUInt32LittleEndian(page[PageHeaderCodec.ChecksumOffset..], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(page[PageHeaderCodec.ChecksumOffset..], Calculate(page));
    }

    public static void ValidateChecksum(ReadOnlySpan<byte> page, int pageSize)
    {
        ValidateSize(page.Length, pageSize);
        var expected = BinaryPrimitives.ReadUInt32LittleEndian(page[PageHeaderCodec.ChecksumOffset..]);
        var crc = 0xffffffffu;
        for (var index = 0; index < page.Length; index++)
        {
            var value = index is >= PageHeaderCodec.ChecksumOffset and < PageHeaderCodec.ChecksumOffset + sizeof(uint)
                ? (byte)0 : page[index];
            crc = Update(crc, value);
        }
        var actual = ~crc;
        if (actual != expected)
            throw new StorageCorruptionException($"Page checksum mismatch: expected 0x{expected:x8}, calculated 0x{actual:x8}.");
    }

    private static uint Calculate(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xffffffffu;
        foreach (var value in bytes) crc = Update(crc, value);
        return ~crc;
    }

    private static uint Update(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        return crc;
    }

    private static void ValidateSize(int actual, int expected)
    {
        if (actual != expected || !PageConstants.IsSupportedSize(expected))
            throw new ArgumentException($"Page must be exactly a supported {expected}-byte page.");
    }
}
