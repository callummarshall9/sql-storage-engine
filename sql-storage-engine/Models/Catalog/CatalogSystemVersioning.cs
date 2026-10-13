// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogSystemVersioning
{
    public TableId HistoryTableId { get; init; }
    public ColumnId PeriodStartColumnId { get; init; }
    public ColumnId PeriodEndColumnId { get; init; }
}