// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Processings.StorageLimitSettings;
internal sealed partial class StorageLimitsProcessingService : IStorageLimitsProcessingService
{
    public StorageLimits CreateStorageLimits(int bufferFrames, int rowBytes, int keyBytes, long valueBytes, int overflowPages, int transactionSeconds, long undoBytes, int pinsPerTransaction, int scanPages, int concurrentTransactions) =>
        TryCatch(operation: () =>
    {
        ValidateStorageLimits(bufferFrames: bufferFrames, rowBytes: rowBytes, keyBytes: keyBytes, valueBytes: valueBytes, overflowPages: overflowPages, transactionDuration: TimeSpan.FromSeconds(seconds: transactionSeconds), undoBytes: undoBytes, pinsPerTransaction: pinsPerTransaction, scanPages: scanPages, concurrentTransactions: concurrentTransactions);

        return new StorageLimits
        {
            BufferFrames = bufferFrames,
            RowBytes = rowBytes,
            KeyBytes = keyBytes,
            ValueBytes = valueBytes,
            OverflowPages = overflowPages,
            TransactionDuration = TimeSpan.FromSeconds(seconds: transactionSeconds),
            UndoBytes = undoBytes,
            PinsPerTransaction = pinsPerTransaction,
            ScanPages = scanPages,
            ConcurrentTransactions = concurrentTransactions
        };
    });

    public StorageLimits PrepareStorageLimits(StorageLimits storageLimits) =>
        TryCatch(operation: () =>
    {
        ValidateStorageLimits(storageLimits: storageLimits);
        return storageLimits;
    });
}