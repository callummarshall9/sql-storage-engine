// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Catalog;
public sealed class CatalogColumnExpressionContext
{
    public string? Expression { get; init; }
    public string? ColumnName { get; init; }
    public IReadOnlyList<CatalogColumn>? Columns { get; init; }
    public int Operation { get; init; }
    public bool ReferencesColumn { get; internal set; }
    public IReadOnlyList<int>? ComputedColumnOrder { get; internal set; }
    public CatalogColumn? DirectColumnReference { get; internal set; }
}