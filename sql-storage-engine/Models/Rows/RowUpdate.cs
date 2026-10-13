// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Rows;
/// <summary>A data contract describing the column changes in one row update.</summary>
public sealed class RowUpdate
{
    private IReadOnlyList<ColumnUpdate>? _columns;
    internal IEnumerable<ColumnUpdate>? ColumnSource { get; init; }
    public IReadOnlyList<ColumnUpdate> Columns { get => _columns!; init => _columns = value; }
}