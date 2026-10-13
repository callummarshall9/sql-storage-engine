// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Storage;
internal sealed record StorageEngineCreationContext
{
    public string? Path { get; init; }
    public StorageEngineOptions? Options { get; init; }
    public bool Creating { get; init; }
}