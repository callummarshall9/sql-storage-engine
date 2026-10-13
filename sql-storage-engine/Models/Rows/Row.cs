// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;

namespace sql_storage_engine.Models.Rows;
/// <summary>Data describing the typed SQL values in one row.</summary>
public sealed class Row
{
    private IReadOnlyList<SqlValue>? _values;
    internal IEnumerable<SqlValue>? ValueSource { get; init; }
    public IReadOnlyList<SqlValue> Values { get => _values!; init => _values = value; }
}