// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Buffers.Binary;
using sql_storage_engine.Logging;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.RecoveryAnalyses;
internal sealed partial class RecoveryAnalysisProcessingService : IRecoveryAnalysisProcessingService
{
    public RecoveryAnalysis PrepareRecoveryAnalysis(RecoveryAnalysis recoveryAnalysis) =>
        TryCatch(operation: () =>
    {
        ValidateRecoveryAnalysis(recoveryAnalysis: recoveryAnalysis);
        var records = recoveryAnalysis.Records.ToArray();
        ValidateRecords(records: records);
        var transactions = recoveryAnalysis.Checkpoint?.ActiveTransactions.Keys.ToDictionary(keySelector: id => id, elementSelector: _ => TransactionState.Active) ?? new Dictionary<TransactionId, TransactionState>();
        var dirty = recoveryAnalysis.Checkpoint is null ? new Dictionary<PageId, LogSequenceNumber>() : new Dictionary<PageId, LogSequenceNumber>(collection: recoveryAnalysis.Checkpoint.DirtyPages);
        ApplyRecords(records: records, transactions: transactions, dirty: dirty);

        return new RecoveryAnalysis
        {
            Transactions = new ReadOnlyDictionary<TransactionId, TransactionState>(dictionary: transactions),
            DirtyPages = new ReadOnlyDictionary<PageId, LogSequenceNumber>(dictionary: dirty),
            Records = Array.AsReadOnly(array: records),
            ValidLength = recoveryAnalysis.ValidLength,
            TruncatedTail = recoveryAnalysis.TruncatedTail
        };
    });

    private static void ApplyRecords(IEnumerable<WalRecord> records, Dictionary<TransactionId, TransactionState> transactions, Dictionary<PageId, LogSequenceNumber> dirty)
    {
        foreach (var record in records)
        {
            if (record.Type == WalRecordType.Checkpoint)
            {
                continue;
            }

            transactions[record.TransactionId] = record.Type switch
            {
                WalRecordType.Commit => TransactionState.Committed,
                WalRecordType.Rollback => TransactionState.RolledBack,
                _ => transactions.GetValueOrDefault(key: record.TransactionId, defaultValue: TransactionState.Active)};

            if (record.Type == WalRecordType.PageChange)
            {
                if (record.Payload.Length < sizeof(ulong))
                {
                    throw new StorageCorruptionException("Page-change WAL payload is truncated.");
                }

                var pageId = new sql_storage_engine.Models.Identifiers.PageId
                {
                    Value = BinaryPrimitives.ReadUInt64LittleEndian(source: record.Payload.Span)
                };

                dirty.TryAdd(key: pageId, value: record.Lsn);
            }
        }
    }
}