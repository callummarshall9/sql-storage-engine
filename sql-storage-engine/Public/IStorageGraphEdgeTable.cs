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
/// <summary>Endpoint-validating graph edge access with bounded directional adjacency lookup.</summary>
public interface IStorageGraphEdgeTable
{
    CatalogTable ReadDefinition();

    CatalogGraphTable ReadGraphDefinition();

    ValueTask<GraphEdgeId> InsertAsync(GraphEndpoints graphEndpoints, Row row, CancellationToken cancellationToken = default);

    ValueTask<StoredGraphEdge?> GetAsync(GraphEdgeId edgeId, CancellationToken cancellationToken = default);

    ValueTask<bool> UpdateAsync(GraphEdgeId edgeId, RowUpdate update, CancellationToken cancellationToken = default);

    ValueTask<bool> ReconnectAsync(GraphEdgeId edgeId, GraphEndpoints graphEndpoints, CancellationToken cancellationToken = default);
    /// <summary>
    /// Changes payload and endpoints in one row transition, preserving edge identity and evaluating
    /// generated values and constraints once. Use a statement-scoped handle for multi-effect atomicity.
    /// </summary>
    ValueTask<bool> UpdateAndReconnectAsync(GraphEdgeId edgeId, GraphEndpoints graphEndpoints, RowUpdate update, CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(GraphEdgeId edgeId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<StoredGraphEdge> TraverseAsync(GraphNodeId nodeId, GraphEdgeDirection direction, GraphTraversalOptions? options = null, CancellationToken cancellationToken = default);
}