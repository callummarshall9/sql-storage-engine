// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Pages;
public sealed record DatabaseHeader
{
    public DatabaseId DatabaseId { get; init; }
    public int PageSize { get; init; }
    public ushort FormatVersion { get; init; }
    public PageId? CatalogRootPageId { get; init; }
    public PageId? FreeListHeadPageId { get; init; }
    public PageId NextPageId { get; init; }
    public bool IsCleanShutdown { get; init; }
}