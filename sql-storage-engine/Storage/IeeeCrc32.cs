using System.Buffers.Binary;

namespace sql_storage_engine.Storage;

/// <summary>Internal IEEE CRC-32 accumulator, preserving the version-one disk polynomial.</summary>
internal static class IeeeCrc32
{
    private static readonly uint[][] tables = CreateTables();

    internal static uint Update(uint crc, ReadOnlySpan<byte> bytes)
    {
        while (bytes.Length >= 8)
        {
            crc ^= BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            crc = tables[7][crc & 255] ^ tables[6][(crc >> 8) & 255] ^
                tables[5][(crc >> 16) & 255] ^ tables[4][crc >> 24] ^
                tables[3][bytes[4]] ^ tables[2][bytes[5]] ^ tables[1][bytes[6]] ^ tables[0][bytes[7]];
            bytes = bytes[8..];
        }
        foreach (var value in bytes) crc = (crc >> 8) ^ tables[0][(crc ^ value) & 255];
        return crc;
    }

    private static uint[][] CreateTables()
    {
        var result = Enumerable.Range(0, 8).Select(_ => new uint[256]).ToArray();
        for (var index = 0; index < 256; index++)
        {
            var crc = (uint)index;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
            result[0][index] = crc;

        }
        for (var slice = 1; slice < 8; slice++)
            for (var index = 0; index < 256; index++)
                result[slice][index] = (result[slice - 1][index] >> 8) ^ result[0][result[slice - 1][index] & 255];
        return result;
    }
}
