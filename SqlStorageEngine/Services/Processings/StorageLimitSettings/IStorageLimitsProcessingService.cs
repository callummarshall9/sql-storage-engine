// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Processings.StorageLimitSettings;
internal interface IStorageLimitsProcessingService
{
    StorageLimits CreateStorageLimits(int bufferFrames, int rowBytes, int keyBytes, long valueBytes, int overflowPages, int transactionSeconds, long undoBytes, int pinsPerTransaction, int scanPages, int concurrentTransactions);

    StorageLimits PrepareStorageLimits(StorageLimits storageLimits);
}