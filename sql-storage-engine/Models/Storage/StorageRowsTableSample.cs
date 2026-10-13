// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Storage;
public sealed record StorageRowsTableSample : StorageTableSample
{
    public long RowCount { get; init; }
}