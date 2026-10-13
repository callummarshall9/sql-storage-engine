// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Storage;
internal sealed record StorageHandleContext
{
    public StorageEngineRuntime? Engine { get; init; }
    public StorageStatementRuntime? Statement { get; init; }
    public long Generation { get; init; }
}