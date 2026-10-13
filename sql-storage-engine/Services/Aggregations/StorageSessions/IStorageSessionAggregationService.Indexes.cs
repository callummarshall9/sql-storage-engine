// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal partial interface IStorageSessionAggregationService
{
    ValueTask<StorageSessionContext> ReadStorageSessionContextIndexAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken);

    StorageSessionContext ReadStorageSessionContextIndexScan(StorageSessionContext storageSessionContext, CancellationToken cancellationToken);
}