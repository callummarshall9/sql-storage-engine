// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal partial interface IStorageSessionAggregationService
{
    ValueTask<StorageSessionContext> BeginStorageSessionContextTransactionAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken);

    ValueTask<StorageSessionContext> ExecuteStorageSessionContextReceiptAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken);

    StorageSessionContext ReadStorageSessionContextTransaction(StorageSessionContext storageSessionContext);

    ValueTask<StorageSessionContext> ExecuteStorageSessionContextTransactionAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken);
}