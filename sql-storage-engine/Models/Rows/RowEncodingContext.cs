// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Models.Rows;
internal sealed record RowEncodingContext
{
    public sql_storage_engine.Models.Graph.GraphRowContext? Graph { get; set; }
    public SqlType? TargetType { get; set; }
    public SqlType? SourceType { get; set; }
    public SqlTypeName ProjectionTypeName { get; set; }
    public TableDefinition? Table { get; set; }
    public Row? Row { get; set; }
    public ColumnDefinition? Column { get; set; }
    public SqlValue? Value { get; set; }
    public ReadOnlyMemory<byte> Source { get; set; }
    public byte[]? Bytes { get; set; }
    public IReadOnlyDictionary<int, OverflowReference>? OverflowReferences { get; set; }
    public IReadOnlyDictionary<ColumnId, SqlValue>? Selected { get; set; }
    public IEnumerable<ColumnId>? ColumnIds { get; set; }
    public RowHeader Header { get; set; }
    public Dictionary<int, RowVariableEntry>? VariableEntries { get; set; }
    public RowUpdate? Update { get; set; }
    public OverflowReference? Reference { get; set; }
    public RowEncodingResult? EncodingResult { get; set; }
    public (double MinX, double MinY, double MaxX, double MaxY) Bounds { get; set; }
    public int Width { get; set; }
    public uint SchemaHash { get; set; }
    public bool IsVariable { get; set; }
    public int InlineThreshold { get; set; }
    public ColumnId ColumnId { get; set; }
    public RowValueStorage ValueStorage { get; set; }
    public IReadOnlyDictionary<ColumnId, OverflowReference>? ReferenceResults { get; set; }
    public string? Text { get; set; }
    public ColumnSetXmlElement[]? ColumnSetElements { get; set; }
}