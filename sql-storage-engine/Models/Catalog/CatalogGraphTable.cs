// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Catalog;
public sealed record CatalogGraphTable
{
    public GraphTableKind Kind { get; init; }
    public ColumnId IdentityColumnId { get; init; }
    public IndexId IdentityIndexId { get; init; }
    public TableId? FromNodeTableId { get; init; }
    public TableId? ToNodeTableId { get; init; }
    public ColumnId? FromNodeColumnId { get; init; }
    public ColumnId? ToNodeColumnId { get; init; }
    public IndexId? OutgoingIndexId { get; init; }
    public IndexId? IncomingIndexId { get; init; }
}