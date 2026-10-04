using System.Buffers.Binary;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>Calculates and validates IEEE CRC-32 over a complete fixed-size page.</summary>
public static class PageChecksum
{
    private static readonly byte[] zeroChecksum = new byte[sizeof(uint)];
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
        var crc = IeeeCrc32.Update(0xffffffffu, page[..PageHeaderCodec.ChecksumOffset]);
        crc = IeeeCrc32.Update(crc, zeroChecksum);
        var actual = ~IeeeCrc32.Update(crc, page[(PageHeaderCodec.ChecksumOffset + sizeof(uint))..]);
        if (actual != expected)
            throw new StorageCorruptionException($"Page checksum mismatch: expected 0x{expected:x8}, calculated 0x{actual:x8}.");
    }

    private static uint Calculate(ReadOnlySpan<byte> bytes) => ~IeeeCrc32.Update(0xffffffffu, bytes);

    private static void ValidateSize(int actual, int expected)
    {
        if (actual != expected || !PageConstants.IsSupportedSize(expected))
            throw new ArgumentException($"Page must be exactly a supported {expected}-byte page.");
    }
}
