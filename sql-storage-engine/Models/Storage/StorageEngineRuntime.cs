// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Models.Storage;
internal sealed class StorageEngineRuntime
{
    public StoragePublicTransactionContext? ActiveTransaction { get; set; }
    public object? ResourceSource { get; set; }
    public bool Disposed { get; set; }
    public bool Quarantined { get; set; }
    public long Generation { get; set; }
    public Task? PendingCleanup { get; set; }
    public Action<string>? TransactionObserver { get; set; }
    public Action<GraphDdlStage>? GraphDdlObserver { get; set; }
    public bool SecurityEventsUncertain { get; set; }
}