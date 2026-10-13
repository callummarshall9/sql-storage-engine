// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Brokers.Checksums;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.Services.Foundations.WalSegmentHeaders;
internal sealed partial class WalSegmentHeaderService : IWalSegmentHeaderService
{
    private static readonly ICrc32Broker crc32 = new Crc32Broker();
    public byte[] WriteWalSegmentHeader(WalSegmentHeader walSegmentHeader) =>
        TryCatch(operation: () =>
    {
        ValidateWalSegmentHeader(walSegmentHeader: walSegmentHeader);
        var bytes = new byte[WalFormatConstants.SegmentHeaderLength];
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes, value: WalFormatConstants.SegmentMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 4), value: WalFormatConstants.Version);
        walSegmentHeader.DatabaseId.Value.TryWriteBytes(destination: bytes.AsSpan(start: 8, length: 16), bigEndian: true, bytesWritten: out _);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: 24), value: walSegmentHeader.Timeline);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: 32), value: walSegmentHeader.SegmentNumber);
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: 44), value: crc32.CalculateChecksum(bytes: bytes.AsSpan(start: 0, length: 44)));
        return bytes;
    });

    public WalSegmentHeader ReadWalSegmentHeader(ReadOnlySpan<byte> source) =>
        TryCatch(operation: static context =>
    {
        ValidateWalSegmentHeaderSource(source: context.source);

        return new WalSegmentHeader
        {
            DatabaseId = new sql_storage_engine.Models.Identifiers.DatabaseId
            {
                Value = new Guid(context.source.Slice(start: 8, length: 16), bigEndian: true)
            },
            Timeline = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[24..]),
            SegmentNumber = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[32..])
        };
    }, context: new ReadableWalFormatContext { source = source });
}