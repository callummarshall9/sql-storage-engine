// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Rows;
internal sealed record ColumnSetXmlElement
{
    public string? NamespaceName { get; init; }
    public string? Name { get; init; }
    public string? Value { get; init; }
    public string? Nil { get; init; }
}