// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Tables;
public sealed record TableStorageBinding
{
    internal object? Service { get; init; }
    internal TableStorageContext? Context { get; init; }
}