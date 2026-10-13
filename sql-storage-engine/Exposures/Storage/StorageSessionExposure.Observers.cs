// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Graph;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure
{
    /// <summary>Binds an optional transaction-stage observer to the shared runtime.</summary>
    internal void WriteTransactionObserver(Action<string>? observer) =>
        service.WriteStorageSessionContextEngineObserver(storageSessionContext: new StorageSessionContext { Engine = engineContext, Observer = new StorageEngineObserverContext { Operation = "Transaction", TransactionObserver = observer } });
    /// <summary>Binds an optional graph-DDL-stage observer to the shared runtime.</summary>
    internal void WriteGraphDdlObserver(Action<GraphDdlStage>? observer) =>
        service.WriteStorageSessionContextEngineObserver(storageSessionContext: new StorageSessionContext { Engine = engineContext, Observer = new StorageEngineObserverContext { Operation = "Graph", GraphDdlObserver = observer } });
    /// <summary>Returns the actual pending callback cleanup task.</summary>
    internal Task? ReadPendingTransactionCleanup() =>
        service.WriteStorageSessionContextEngineObserver(storageSessionContext: new StorageSessionContext { Engine = engineContext, Observer = new StorageEngineObserverContext { Operation = "Pending" } })
            .Observer!.PendingCleanup;
}