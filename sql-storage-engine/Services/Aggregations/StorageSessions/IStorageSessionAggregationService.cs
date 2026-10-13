// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading.Tasks;
using System.Threading;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal partial interface IStorageSessionAggregationService
{
    StorageSessionContext ReadStorageSessionContextCatalog(StorageSessionContext storageSessionContext);

    ValueTask<StorageSessionContext> ExecuteStorageSessionContextCatalogAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken);
}