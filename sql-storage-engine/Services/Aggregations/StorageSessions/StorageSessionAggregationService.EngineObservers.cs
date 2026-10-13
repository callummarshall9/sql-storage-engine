// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public StorageSessionContext WriteStorageSessionContextEngineObserver(StorageSessionContext storageSessionContext) =>
        TryCatch(operation: () =>
    {
        ValidatePublicTableInputs(inputs: [(storageSessionContext is null, nameof(storageSessionContext)), (storageSessionContext?.Engine is null, nameof(StorageSessionContext.Engine)), (storageSessionContext?.Observer is null, nameof(StorageSessionContext.Observer))]);
        var request = storageSessionContext!.Observer!;

        switch (request.Operation)
        {
            case "Transaction":
                storageSessionContext.Engine!.TransactionObserver = request.TransactionObserver;
                break;
            case "Graph":
                storageSessionContext.Engine!.GraphDdlObserver = request.GraphDdlObserver;
                break;
            case "Pending":
                request.PendingCleanup = storageSessionContext.Engine!.PendingCleanup;
                break;
            default:
                throw new ArgumentException(message: "Unknown runtime observer request.", paramName: nameof(StorageEngineObserverContext.Operation));
        }

        return storageSessionContext;
    });
}