// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Storage;
public sealed record StoragePercentTableSample : StorageTableSample
{
    public decimal Percentage { get; init; }
}