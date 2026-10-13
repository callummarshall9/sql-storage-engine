// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
internal readonly record struct RowVariableEntry
{
    public RowValueStorage Storage { get; init; }
    public int Offset { get; init; }
    public int Length { get; init; }
}