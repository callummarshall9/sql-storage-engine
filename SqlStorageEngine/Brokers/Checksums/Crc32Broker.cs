// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.IO.Hashing;

namespace sql_storage_engine.Brokers.Checksums;
/// <summary>Adapts the framework IEEE CRC-32 algorithm to the durable page and WAL checksum contract.</summary>
internal sealed class Crc32Broker : ICrc32Broker
{
    private static readonly byte[] zeroChecksum = new byte[sizeof(uint)];
    public uint CalculateChecksum(ReadOnlySpan<byte> bytes) =>
        Crc32.HashToUInt32(source: bytes);

    public uint CalculateChecksumWithZeroedField(ReadOnlySpan<byte> bytes, int zeroOffset)
    {
        var crc = new Crc32();
        crc.Append(source: bytes[..zeroOffset]);
        crc.Append(source: zeroChecksum);
        crc.Append(source: bytes[(zeroOffset + sizeof(uint))..]);
        return crc.GetCurrentHashAsUInt32();
    }
}