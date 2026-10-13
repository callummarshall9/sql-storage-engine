// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal partial interface IStorageSessionAggregationService
{
    StorageSessionContext ReadStorageSessionContextEngineCatalog(StorageSessionContext storageSessionContext);

    ValueTask<StorageSessionContext> ExecuteStorageSessionContextEngineCatalogAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken);
}