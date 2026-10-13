// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Services.Foundations.WalRecords;

namespace sql_storage_engine.Services.Processings.CompatibilityWalRecords;
internal sealed partial class WalRecordCompatibilityContextProcessingService : IWalRecordCompatibilityContextProcessingService
{
    private readonly WalRecordService records = new WalRecordService();
    public CompatibilityContext EncodeCompatibilityContext(CompatibilityContext compatibilityContext) =>
        TryCatch(operation: () =>
    {
        ValidateCompatibilityContext(compatibilityContext: compatibilityContext);

        var encoded = compatibilityContext.Fixture!.WalRecords.Select(selector: record => new WalRecord { Lsn = new LogSequenceNumber { Value = record.Lsn }, PreviousLsn = default, TransactionId = new TransactionId { Value = record.TransactionId }, Type = Enum.Parse<WalRecordType>(value: record.Type), Payload = ReadOnlyMemory<byte>.Empty })
            .SelectMany(selector: records.WriteWalRecord)
            .ToArray();

        return compatibilityContext with
        {
            Bytes = encoded
        };
    });
}