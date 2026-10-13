// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Storage;
public abstract record StorageTableSample
{
    public long? RepeatableSeed { get; init; }
}