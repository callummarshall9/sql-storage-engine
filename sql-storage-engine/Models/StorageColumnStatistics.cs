// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models;
/// <summary>Exact cardinality metadata captured from a validated storage snapshot.</summary>
public sealed record StorageColumnStatistics
{
    public ColumnId ColumnId { get; init; }
    public long NullCount { get; init; }
    public long DistinctValueCount { get; init; }
}