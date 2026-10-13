// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogIndexDocumentContext
{
    public int Operation { get; init; }
    public string? Json { get; init; }
    public string? Path { get; init; }
    public string? NormalizedPath { get; init; }
    public string? Text { get; set; }
    public byte[]? Bytes { get; set; }
    public (int Kind, string Raw, string? Text)[]? Values { get; set; }
    public (string Path, int Kind, string Raw, string? Text)[]? Descendants { get; set; }
}