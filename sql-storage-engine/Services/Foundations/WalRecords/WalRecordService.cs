// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using sql_storage_engine.Brokers.Checksums;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Logging;

namespace sql_storage_engine.Services.Foundations.WalRecords;
internal sealed partial class WalRecordService : IWalRecordService
{
    private static readonly ICrc32Broker crc32 = new Crc32Broker();
    public byte[] WriteWalRecord(WalRecord walRecord) =>
        TryCatch(operation: () =>
    {
        ValidateWalRecord(walRecord: walRecord);
        var bytes = new byte[checked(WalFormatConstants.RecordHeaderLength + walRecord.Payload.Length)];
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes, value: checked((uint)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 4), value: WalFormatConstants.Version);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: bytes.AsSpan(start: 6), value: (ushort)walRecord.Type);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: 8), value: walRecord.Lsn.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: 16), value: walRecord.PreviousLsn.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: bytes.AsSpan(start: 24), value: walRecord.TransactionId.Value);
        walRecord.Payload.Span.CopyTo(destination: bytes.AsSpan(start: WalFormatConstants.RecordHeaderLength));
        BinaryPrimitives.WriteUInt32LittleEndian(destination: bytes.AsSpan(start: 36), value: crc32.CalculateChecksumWithZeroedField(bytes: bytes, zeroOffset: 36));
        return bytes;
    });

    public WalRecord ReadWalRecord(ReadOnlySpan<byte> source) =>
        TryCatch(operation: static context =>
    {
        ValidateWalRecordSource(source: context.source);

        return new WalRecord
        {
            Lsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[8..])
            },
            PreviousLsn = new sql_storage_engine.Models.Identifiers.LogSequenceNumber
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[16..])
            },
            TransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: context.source[24..])
            },
            Type = (WalRecordType)BinaryPrimitives.ReadUInt16LittleEndian(source: context.source[6..]),
            Payload = context.source[WalFormatConstants.RecordHeaderLength..].ToArray()
        };
    }, context: new ReadableWalFormatContext { source = source });
}