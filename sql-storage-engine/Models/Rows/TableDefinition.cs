// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Rows;
public sealed record TableDefinition
{
    public IReadOnlyList<ColumnDefinition> Columns { get => columns!; init => columns = value; }

    private IReadOnlyList<ColumnDefinition>? columns;
    internal IEnumerable<ColumnDefinition>? ColumnSource { get; init; }
    internal TableDefinition? SchemaSource { get; init; }
    internal Row? RowInput { get; init; }
}