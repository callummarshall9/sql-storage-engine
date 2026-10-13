// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal partial interface IStorageSessionAggregationService
{
    StorageSessionContext WriteStorageSessionContextEngineObserver(StorageSessionContext storageSessionContext);
}