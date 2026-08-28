using sql_storage_engine.Catalog;
using sql_storage_engine.Rows;

namespace sql_storage_engine;

/// <summary>Endpoint-safe graph node access; graph tables reject ordinary mutation handles.</summary>
public interface IStorageGraphNodeTable
{
    CatalogTable Definition { get; }
    CatalogGraphTable GraphDefinition { get; }
    ValueTask<GraphNodeId> InsertAsync(Row row, CancellationToken cancellationToken = default);
    ValueTask<StoredGraphNode?> GetAsync(GraphNodeId nodeId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<StoredGraphNode> ScanAsync(CancellationToken cancellationToken = default);
    ValueTask<bool> UpdateAsync(GraphNodeId nodeId, RowUpdate update,
        CancellationToken cancellationToken = default);
    ValueTask<bool> DeleteAsync(GraphNodeId nodeId, CancellationToken cancellationToken = default);
}
