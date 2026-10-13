// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Models.Catalog;
internal sealed class CatalogBufferContext
{
    public int Operation { get; init; }
    public ulong PageId { get; init; }
}