// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogBTreeIndexOptions
{
    public CatalogIndexStorageKind StorageKind { get; init; }
    public bool IsPrimaryKey { get; init; }
    public IReadOnlyList<ColumnId> IncludedColumns { get => _includedColumns!; init => _includedColumns = value; }

    private IReadOnlyList<ColumnId>? _includedColumns;
    public bool IgnoreDuplicateKey { get; init; }
    public IEnumerable<ColumnId>? IncludedColumnSource { get; init; }
}