// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Storage;
public sealed record StorageSessionBinding
{
    internal StorageEngineRuntime? Engine { get; init; }
    internal object? Service { get; init; }
}