// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Models.Storage;
internal sealed record StoragePublicSavepointEntry
{
    public StorageSavepoint? Token { get; init; }
    public EngineResourceContext? Journal { get; init; }
    public long Bytes { get; init; }
}