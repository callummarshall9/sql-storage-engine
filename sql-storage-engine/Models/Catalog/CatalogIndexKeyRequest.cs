// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Catalog;
public sealed class CatalogIndexKeyRequest
{
    public SqlType? Type { get; set; }
    public string? DefaultCollation { get; set; }
    public int Operation { get; init; }
    public Row? Row { get; init; }
    public CatalogTable? Table { get; init; }
    public CatalogIndex? Index { get; init; }
    public string? Path { get; init; }
    public SqlValue? SqlValue { get; init; }
    public int MaximumKeyBytes { get; init; }
    public string? Value { get; init; }
    public FullTextSearchRequest? Request { get; init; }
    public byte[]? Payload { get; init; }
    public IReadOnlyList<SqlValue>? Values { get; init; }
    public IReadOnlyList<IndexKey>? Entries { get; internal set; }
    public IndexKey? Key { get; internal set; }
    public byte[]? PayloadResult { get; internal set; }
    public IReadOnlyDictionary<ColumnId, SqlValue>? IncludedValues { get; internal set; }
}