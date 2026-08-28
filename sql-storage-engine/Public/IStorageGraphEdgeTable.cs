using sql_storage_engine.Catalog;
using sql_storage_engine.Rows;

namespace sql_storage_engine;

/// <summary>Endpoint-validating graph edge access with bounded directional adjacency lookup.</summary>
public interface IStorageGraphEdgeTable
{
    CatalogTable Definition { get; }
    CatalogGraphTable GraphDefinition { get; }
    ValueTask<GraphEdgeId> InsertAsync(GraphNodeId fromNodeId, GraphNodeId toNodeId, Row row,
        CancellationToken cancellationToken = default);
    ValueTask<StoredGraphEdge?> GetAsync(GraphEdgeId edgeId, CancellationToken cancellationToken = default);
    ValueTask<bool> UpdateAsync(GraphEdgeId edgeId, RowUpdate update,
        CancellationToken cancellationToken = default);
    ValueTask<bool> ReconnectAsync(GraphEdgeId edgeId, GraphNodeId fromNodeId, GraphNodeId toNodeId,
        CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(GraphEdgeId edgeId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<StoredGraphEdge> TraverseAsync(GraphNodeId nodeId, GraphEdgeDirection direction,
        GraphTraversalOptions? options = null, CancellationToken cancellationToken = default);
}
