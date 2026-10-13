// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Rows;
internal sealed record SqlExpressionContext
{
    public System.Collections.Generic.IReadOnlyList<sql_storage_engine.Models.Catalog.CatalogColumn>? ColumnDefinitions { get; init; }
    public System.Collections.Generic.IReadOnlyList<int>? ComputedColumnOrder { get; set; }
    public sql_storage_engine.Models.Catalog.CatalogColumn? DirectColumnReference { get; set; }
    public string? Source { get; init; }
    public IReadOnlyDictionary<string, SqlValue>? Columns { get; init; }
    public SqlExpressionToken[]? Tokens { get; set; }
    public int Position { get; set; }
    public SqlExpressionToken Token { get; set; }
    public ExpressionTokenKind Expected { get; set; }
    public bool Consumed { get; set; }
    public SqlValue? Result { get; set; }
    public Func<string, object, object>? Runtime { get; init; }
    public bool HostHandled { get; set; }
    public object? TimeProviderSource { get; init; }
    public DateTime Timestamp { get; set; }
    public Guid Identifier { get; set; }
    public bool Utc { get; init; }
}