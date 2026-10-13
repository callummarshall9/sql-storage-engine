// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Graph;

namespace sql_storage_engine;
/// <summary>Endpoint-safe graph node access; graph tables reject ordinary mutation handles.</summary>
public interface IStorageGraphNodeTable
{
    CatalogTable ReadDefinition();

    CatalogGraphTable ReadGraphDefinition();

    ValueTask<GraphNodeId> InsertAsync(Row row, CancellationToken cancellationToken = default);

    ValueTask<StoredGraphNode?> GetAsync(GraphNodeId nodeId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StoredGraphNode> ScanAsync(CancellationToken cancellationToken = default);

    ValueTask<bool> UpdateAsync(GraphNodeId nodeId, RowUpdate update, CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(GraphNodeId nodeId, CancellationToken cancellationToken = default);
}