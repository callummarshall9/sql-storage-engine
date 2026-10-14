// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Logging;
using System.Buffers.Binary;
using System.Collections.Generic;
using sql_storage_engine.Services.Foundations.WalRecords;

namespace sql_storage_engine.Services.Processings.WalRecordContexts;
internal sealed partial class WalRecordWalAppendContextCodecProcessingService(WalRecordService records) : IWalRecordWalAppendContextCodecProcessingService
{
    internal static WalRecordWalAppendContextCodecProcessingService CreateWalRecordWalAppendContextCodecProcessingService() =>
        new WalRecordWalAppendContextCodecProcessingService(records: new WalRecordService());

    public WalAppendContext WriteWalAppendContext(WalAppendContext walAppendContext) =>
        TryCatch(operation: () =>
    {
        ValidateRecord(walAppendContext: walAppendContext);

        return walAppendContext with
        {
            EncodedRecord = records.WriteWalRecord(walRecord: walAppendContext.Record!)
        };
    });

    public WalAppendContext ReadWalAppendContext(WalAppendContext walAppendContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(walAppendContext: walAppendContext);

        return walAppendContext with
        {
            ReadResult = ReadCore(source: walAppendContext.Source!)
        };
    });

    private WalReadResult ReadCore(ReadOnlySpan<byte> source)
    {
        List<WalRecord> decodedRecords = [];
        var offset = 0;

        while (offset < source.Length)
        {
            if (source.Length - offset < sizeof(uint))
            {
                return CreateWalReadResult(records: decodedRecords, incomplete: true, validLength: offset);
            }

            var length = BinaryPrimitives.ReadUInt32LittleEndian(source: source[offset..]);
            ValidateRecordLength(length: length);

            if (length > source.Length - offset)
            {
                return CreateWalReadResult(records: decodedRecords, incomplete: true, validLength: offset);
            }

            decodedRecords.Add(item: records.ReadWalRecord(source: source.Slice(start: offset, length: (int)length)));
            offset = checked(offset + (int)length);
        }

        return CreateWalReadResult(records: decodedRecords, incomplete: false, validLength: offset);
    }

    private static WalReadResult CreateWalReadResult(List<WalRecord> records, bool incomplete, int validLength) =>
        new()
    {
        Records = records.AsReadOnly(),
        HasIncompleteTail = incomplete,
        ValidLength = validLength
    };
}