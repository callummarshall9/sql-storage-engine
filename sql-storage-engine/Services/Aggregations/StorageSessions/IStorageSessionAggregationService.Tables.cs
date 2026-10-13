// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal partial interface IStorageSessionAggregationService
{
    ValueTask<StorageSessionContext> AddStorageSessionContextTableAsync(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken);

    ValueTask<StorageSessionContext> AddStorageSessionContextTableTryAsync(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken);

    ValueTask<StorageSessionContext> AddStorageSessionContextTableGraphAsync(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken);

    ValueTask<StorageSessionContext> AddStorageSessionContextTablePreparedAsync(StorageSessionContext newStorageSessionContext, CancellationToken cancellationToken);

    ValueTask<StorageSessionContext> UpdateStorageSessionContextTableAsync(StorageSessionContext updatedStorageSessionContext, CancellationToken cancellationToken);

    ValueTask<StorageSessionContext> UpdateStorageSessionContextTableGraphAsync(StorageSessionContext updatedStorageSessionContext, CancellationToken cancellationToken);

    ValueTask<StorageSessionContext> UpdateStorageSessionContextTableSystemVersionedAsync(StorageSessionContext updatedStorageSessionContext, CancellationToken cancellationToken);

    ValueTask<StorageSessionContext> DeleteStorageSessionContextTableAsync(StorageSessionContext deletedStorageSessionContext, CancellationToken cancellationToken);

    ValueTask<StorageSessionContext> ReadStorageSessionContextTableAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken);

    StorageSessionContext ReadStorageSessionContextTableScan(StorageSessionContext storageSessionContext, CancellationToken cancellationToken);

    StorageSessionContext ReadStorageSessionContextTableSample(StorageSessionContext storageSessionContext, CancellationToken cancellationToken);

    StorageSessionContext ConfigureStorageSessionContextTableTransaction(StorageSessionContext storageSessionContext);

    StorageSessionContext ReadStorageSessionContextTableDefinition(StorageSessionContext storageSessionContext);
}