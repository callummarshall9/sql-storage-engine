// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Threading.Tasks;

namespace sql_storage_engine.Models.Storage;
internal sealed class StoragePublicTransactionContext
{
    public StorageEngineRuntime? Engine { get; init; }
    public EngineResourceContext? Lease { get; set; }
    public EngineResourceContext? Journal { get; set; }
    public EngineResourceContext? Runtime { get; set; }
    public StorageTransactionIdentity Identity { get; init; }
    public StorageTransactionState State { get; set; }
    public StorageTransactionOptions? Options { get; init; }
    public List<StoragePublicSavepointEntry>? Savepoints { get; init; }
    public long SavepointBytes { get; set; }
    public bool Disposed { get; set; }
    public bool Released { get; set; }
    public Task? TimeoutCleanup { get; set; }
}