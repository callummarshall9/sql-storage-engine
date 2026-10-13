// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Rows;
public readonly record struct ColumnUpdate
{
    public int ColumnIndex { get; init; }

    private readonly SqlValue? _value;
    public SqlValue Value { get => _value!; init => _value = value; }
}