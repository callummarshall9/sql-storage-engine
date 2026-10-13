// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Graph;
internal sealed record GraphRowContext
{
    public GraphTraversalOptions? Options { get; init; }
    public string? Operation { get; init; }
    public GraphIdentityValue? Identity { get; set; }
    public Row? Row { get; set; }
    public RowUpdate? Update { get; init; }
    public SqlValue[]? Generated { get; init; }
    public int PayloadColumnCount { get; init; }
    public int GeneratedCount { get; init; }
    public CatalogTable? Table { get; init; }
    public DatabaseId DatabaseId { get; init; }
    public GraphNodeId NodeId { get; init; }
    public GraphEdgeId EdgeId { get; init; }
    public string? ParameterName { get; init; }
}