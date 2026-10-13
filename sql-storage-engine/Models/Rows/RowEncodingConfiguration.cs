// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Rows;
internal sealed record RowEncodingConfiguration
{
    public object? OverflowService { get; set; }
    public bool OverflowSourceBound { get; set; }
    public bool UseOverflow { get; set; }
    public int InlineThreshold { get; set; }
}