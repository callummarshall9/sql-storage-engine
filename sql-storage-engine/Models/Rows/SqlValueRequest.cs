// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Rows;
internal sealed record SqlValueRequest
{
    public string Operation { get => operation!; init => operation = value; }

    private string? operation;
    public SqlValue[]? SqlValues { get; init; }
    public byte[]? BytesResult { get; init; }
    public System.Collections.Generic.IReadOnlyList<float>? FloatResult { get; init; }
    public System.Collections.Generic.IReadOnlyList<Half>? HalfResult { get; init; }
    public double DistanceResult { get; init; }
    public object? Input { get; init; }
    public object? Other { get; init; }
    public object? Third { get; init; }
    public int Srid { get; init; }
    public SqlValue? Result { get; init; }
    public sql_storage_engine.Rows.SqlComparison Comparison { get; init; }
    public TableDefinition? DefinitionResult { get; init; }
}