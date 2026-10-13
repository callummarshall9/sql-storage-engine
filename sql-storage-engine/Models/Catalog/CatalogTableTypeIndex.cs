// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Catalog;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogTableTypeIndex
{
    public string Name { get => name!; internal set => name = value; }

    private string? name;
    public bool IsPrimaryKey { get; internal set; }
    public bool IsUnique { get; internal set; }
    public IReadOnlyList<CatalogIndexedColumn> Columns { get => columns!; internal set => columns = value; }

    private IReadOnlyList<CatalogIndexedColumn>? columns;
    public CatalogIndexStorageKind StorageKind { get; internal set; }
    public IReadOnlyList<ColumnId> IncludedColumns { get => includedColumns!; internal set => includedColumns = value; }

    private IReadOnlyList<ColumnId>? includedColumns;
    public bool IgnoreDuplicateKey { get; internal set; }
    public long? BucketCount { get; internal set; }
    internal string NameSource { get => nameSource!; init => nameSource = value; }

    private string? nameSource;
    internal bool IsPrimaryKeySource { get; init; }
    internal bool IsUniqueSource { get; init; }
    internal IEnumerable<CatalogIndexedColumn> ColumnsSource { get => columnsSource!; init => columnsSource = value; }

    private IEnumerable<CatalogIndexedColumn>? columnsSource;
    internal CatalogIndexStorageKind StorageKindSource { get; init; }
    internal IEnumerable<ColumnId>? IncludedColumnsSource { get; init; }
    internal bool IgnoreDuplicateKeySource { get; init; }
    internal long? BucketCountSource { get; init; }
}