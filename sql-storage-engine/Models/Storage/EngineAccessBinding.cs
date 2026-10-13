// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Storage;
internal sealed record EngineAccessBinding
{
    internal object? Service { get; init; }
}