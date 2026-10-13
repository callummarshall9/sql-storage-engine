// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Rows;
internal sealed record RowPayloadContext
{
    public ColumnDefinition? Column { get; set; }
    public SqlValue? Value { get; set; }
    public byte[]? Source { get; set; }
    public byte[]? Bytes { get; set; }
    public (double MinX, double MinY, double MaxX, double MaxY) Bounds { get; set; }
    public int Width { get; set; }
    public string? Text { get; set; }
    public ColumnSetXmlElement[]? ColumnSetElements { get; set; }
}