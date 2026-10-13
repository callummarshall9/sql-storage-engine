// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.Models.Storage;
internal sealed record StorageGraphContext
{
    public StorageHandleContext? Handle { get; init; }
    public TableStorageContext? Storage { get; init; }
    public GraphEndpoints? Endpoints { get; init; }
    public GraphTableEndpoints? TableEndpoints { get; init; }
    public byte Kind { get; init; }
    public string? Operation { get; init; }
    public Row? Row { get; init; }
    public RowUpdate? Update { get; init; }
    public GraphNodeId NodeId { get; set; }
    public GraphEdgeId EdgeId { get; set; }
    public GraphNodeId FromNodeId { get; init; }
    public GraphNodeId ToNodeId { get; init; }
    public GraphEdgeDirection Direction { get; init; }
    public GraphTraversalOptions? Options { get; init; }
    public StoredGraphNode? Node { get; set; }
    public StoredGraphEdge? Edge { get; set; }
    public bool Result { get; set; }
    public IAsyncEnumerable<StoredGraphNode>? Nodes { get; set; }
    public IAsyncEnumerable<StoredGraphEdge>? Edges { get; set; }
    public CatalogTableName? Name { get; init; }
    public IEnumerable<CatalogColumn>? PayloadColumns { get; init; }
    public TableId? FromTableId { get; init; }
    public TableId? ToTableId { get; init; }
    public CatalogTable? CreatedTable { get; set; }
}