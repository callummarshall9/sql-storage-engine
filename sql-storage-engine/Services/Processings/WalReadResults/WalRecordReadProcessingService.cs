// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Services.Foundations.WalRecords;

namespace sql_storage_engine.Services.Processings.WalReadResults;
internal sealed partial class WalRecordReadProcessingService(WalRecordService walRecords) : IWalRecordReadProcessingService
{
    public WalReadResult ReadWalReadResult(ReadOnlySpan<byte> source) =>
        TryCatch(operation: context =>
    {
        ValidateWalReadResultSource(source: context.source);
        return ReadCore(source: context.source);
    }, context: new ReadableWalFormatContext { source = source });

    private WalReadResult ReadCore(ReadOnlySpan<byte> source)
    {
        List<WalRecord> records = [];
        var offset = 0;

        while (offset < source.Length)
        {
            if (source.Length - offset < sizeof(uint))
            {
                return CreateWalReadResult(records: records, incomplete: true, validLength: offset);
            }

            var length = BinaryPrimitives.ReadUInt32LittleEndian(source: source[offset..]);
            ValidateRecordLength(length: length);

            if (length > source.Length - offset)
            {
                return CreateWalReadResult(records: records, incomplete: true, validLength: offset);
            }

            records.Add(item: walRecords.ReadWalRecord(source: source.Slice(start: offset, length: (int)length)));
            offset = checked(offset + (int)length);
        }

        return CreateWalReadResult(records: records, incomplete: false, validLength: offset);
    }

    private static WalReadResult CreateWalReadResult(List<WalRecord> records, bool incomplete, int validLength) =>
        new()
    {
        Records = records.AsReadOnly(),
        HasIncompleteTail = incomplete,
        ValidLength = validLength
    };

    internal static WalRecordReadProcessingService CreateWalRecordReadProcessingService() =>
        new WalRecordReadProcessingService(walRecords: new sql_storage_engine.Services.Foundations.WalRecords.WalRecordService());

    public byte[] WriteWalReadResult(WalReadResult walReadResult) =>
        TryCatch(operation: () =>
    {
        ValidateWalReadResult(walReadResult: walReadResult);
        var output = new List<byte>();

        foreach (var record in walReadResult.Records)
        {
            output.AddRange(collection: walRecords.WriteWalRecord(walRecord: record));
        }

        return output.ToArray();
    });
}