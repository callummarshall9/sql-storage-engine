// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogIndexedColumn
{
    public ColumnId ColumnId { get; init; }
    public SortDirection Direction { get; init; }
    public NullSortOrder NullSortOrder { get; init; }
    public string? Collation { get; init; }
}