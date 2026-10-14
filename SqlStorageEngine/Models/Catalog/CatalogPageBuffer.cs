// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogPageBuffer
{
    public ulong PageId { get; init; }
    public byte[]? Bytes { get; init; }
}