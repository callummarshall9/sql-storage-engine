// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading.Tasks;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.Models.Storage;
internal sealed record StorageEngineObserverContext
{
    public string? Operation { get; init; }
    public Action<string>? TransactionObserver { get; init; }
    public Action<GraphDdlStage>? GraphDdlObserver { get; init; }
    public Task? PendingCleanup { get; set; }
}