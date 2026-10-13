// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Models.Storage;
internal sealed record StorageIndexContext
{
    public StorageHandleContext? Handle { get; init; }
    public TableIndexBinding? Index { get; init; }
    public CatalogTable? Table { get; init; }
    public string? Operation { get; init; }
    public IReadOnlyList<SqlValue>? Values { get; init; }
    public IReadOnlyList<SqlValue>? LowerValues { get; init; }
    public IReadOnlyList<SqlValue>? UpperValues { get; init; }
    public bool IncludeLower { get; init; }
    public bool IncludeUpper { get; init; }
    public ScanDirection Direction { get; init; }
    public StorageIndexRange? Range { get; init; }
    public SqlValue? Query { get; init; }
    public int Count { get; init; }
    public string? Path { get; init; }
    public string? Text { get; init; }
    public FullTextSearchRequest? FullText { get; init; }
    public IReadOnlyList<RowId>? RowIds { get; set; }
    public IReadOnlyList<StorageIndexEntry>? Entries { get; set; }
    public IReadOnlyList<SpecializedIndexMatch>? Matches { get; set; }
    public IAsyncEnumerable<RowId>? RowStream { get; set; }
    public IAsyncEnumerable<StorageIndexEntry>? EntryStream { get; set; }
    public IAsyncEnumerable<FullTextIndexMatch>? FullTextStream { get; set; }
}