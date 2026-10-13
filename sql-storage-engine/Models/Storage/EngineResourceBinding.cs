// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Storage;
public sealed record EngineResourceBinding
{
    internal object? Service { get; init; }
    internal EngineResourceContext? Context { get; init; }
}