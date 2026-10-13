// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Storage;
internal sealed class StorageStatementRuntime
{
    public StoragePublicTransactionContext? Transaction { get; init; }
    public bool Active { get; set; }
    public bool DdlFailed { get; set; }
}