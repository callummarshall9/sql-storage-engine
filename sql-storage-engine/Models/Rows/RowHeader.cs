// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Rows;
internal readonly record struct RowHeader
{
    public ushort Version { get; init; }
    public ushort ColumnCount { get; init; }
    public ushort NullBitmapLength { get; init; }
    public ushort VariableCount { get; init; }
    public uint FixedDataLength { get; init; }
    public uint VariableTableOffset { get; init; }
    public uint VariableDataOffset { get; init; }
    public uint TotalLength { get; init; }
    public uint SchemaHash { get; init; }
}