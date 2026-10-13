// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Storage;
internal sealed record StoragePublicTableContext
{
    public StorageHandleContext? Handle { get; init; }
    public TableStorageContext? Storage { get; init; }
    public bool Coordinate { get; init; }
    public bool FlushMutations { get; init; }
    public string? Operation { get; init; }
    public Row? Row { get; init; }
    public RowId RowId { get; init; }
    public RowUpdate? Update { get; init; }
    public StorageReadOptions? ReadOptions { get; init; }
    public StorageTableSample? Sample { get; init; }
    public StorageTemporalQuery? TemporalQuery { get; init; }
    public StoredRow? StoredRow { get; set; }
    public TableInsertResult? InsertResult { get; set; }
    public TableUpdateResult? UpdateResult { get; set; }
    public TableDeleteResult? DeleteResult { get; set; }
    public IAsyncEnumerable<StoredRow>? Rows { get; set; }
    public IAsyncEnumerable<StorageTemporalRow>? TemporalRows { get; set; }
}