// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogTextEncodingContext
{
    public string? Text { get; set; }
    public byte[]? Bytes { get; set; }
}