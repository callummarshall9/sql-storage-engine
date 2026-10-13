// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogIndex
{
    public IndexId Id { get; init; }
    public string Name { get => name!; init => name = value; }

    private string? name;
    public TableId TableId { get; init; }
    public PageId RootPageId { get; init; }
    public bool IsUnique { get; init; }
    public IReadOnlyList<CatalogIndexedColumn> Columns { get => columns!; init => columns = value; }

    private IReadOnlyList<CatalogIndexedColumn>? columns;
    public CatalogIndexMethod Method { get; init; }
    public CatalogSpecializedIndexOptions? SpecializedOptions { get; init; }
    public CatalogIndexStorageKind StorageKind { get; init; }
    public bool IsPrimaryKey { get; init; }
    public IReadOnlyList<ColumnId> IncludedColumns { get => includedColumns!; init => includedColumns = value; }

    private IReadOnlyList<ColumnId>? includedColumns;
    public bool IgnoreDuplicateKey { get; init; }
    internal IEnumerable<CatalogIndexedColumn>? ColumnSource { get; init; }
    internal IEnumerable<ColumnId>? IncludedColumnSource { get; init; }
}