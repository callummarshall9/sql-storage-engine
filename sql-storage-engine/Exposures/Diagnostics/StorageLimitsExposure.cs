// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Processings.StorageLimitSettings;

namespace sql_storage_engine.Exposures.Diagnostics;
public static class StorageLimitsExposure
{
    private static readonly IStorageLimitsProcessingService service = new StorageLimitsProcessingService();
    public static StorageLimits CreateStorageLimits(int bufferFrames = 1024, int rowBytes = 1024 * 1024, int keyBytes = 4096, long valueBytes = 16L * 1024 * 1024, int overflowPages = 2048, int transactionSeconds = 300, long undoBytes = 64L * 1024 * 1024, int pinsPerTransaction = 1024, int scanPages = 8192, int concurrentTransactions = 1024) =>
        service.CreateStorageLimits(bufferFrames: bufferFrames, rowBytes: rowBytes, keyBytes: keyBytes, valueBytes: valueBytes, overflowPages: overflowPages, transactionSeconds: transactionSeconds, undoBytes: undoBytes, pinsPerTransaction: pinsPerTransaction, scanPages: scanPages, concurrentTransactions: concurrentTransactions);

    public static StorageLimits PrepareStorageLimits(StorageLimits storageLimits) =>
        service.PrepareStorageLimits(storageLimits: storageLimits);
}