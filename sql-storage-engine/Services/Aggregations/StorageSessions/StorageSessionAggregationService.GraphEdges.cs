// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private async ValueTask<GraphEdgeId> GraphEdgeInsertAsync(StorageGraphContext graphContext, GraphNodeId fromNodeId, GraphNodeId toNodeId, Row row, CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);
        ValidateGraphPayload(row: row, table: graphContext.Storage!.Definition!, generatedCount: 3, parameterName: nameof(row));

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        await GraphEdgeEnsureNodeExistsAsync(graphContext: graphContext, nodeId: fromNodeId, expectedTableId: graphContext.Storage!.Definition!.Graph!.FromNodeTableId!.Value, parameterName: nameof(fromNodeId), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await GraphEdgeEnsureNodeExistsAsync(graphContext: graphContext, nodeId: toNodeId, expectedTableId: graphContext.Storage!.Definition!.Graph!.ToNodeTableId!.Value, parameterName: nameof(toNodeId), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var edgeId = CreateGraphEdgeId(databaseId: ReadGraphDatabaseId(graphContext: graphContext), tableId: graphContext.Storage!.Definition!.Id, schemaVersion: graphContext.Storage!.Definition!.SchemaVersion, value: CreateGuidGraphIdentifier());

        var result = await InsertGraphRowAsync(tableStorageContext: graphContext.Storage!, row: AppendGraphPayload(payload: row, generated: [EncodeGraphEdgeId(graphEdgeId: edgeId), EncodeGraphNodeId(graphNodeId: fromNodeId), EncodeGraphNodeId(graphNodeId: toNodeId)]), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!result.Inserted)
        {
            throw new DuplicateKeyIgnoredException(result.Warnings.Single());
        }

        await PublishGraphAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return edgeId;
    }

    private async ValueTask<StoredGraphEdge?> GraphEdgeGetAsync(StorageGraphContext graphContext, GraphEdgeId edgeId, CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);
        ValidateGraphEdgeOwnership(edgeId: edgeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: graphContext.Storage!.Definition!, parameterName: nameof(edgeId));

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        var rowId = await GraphEdgeFindAsync(graphContext: graphContext, edgeId: edgeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return rowId is null ? null : await GraphEdgeReadAsync(graphContext: graphContext, rowId: rowId.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask<bool> GraphEdgeUpdateAsync(StorageGraphContext graphContext, GraphEdgeId edgeId, RowUpdate update, CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);
        ValidateGraphEdgeOwnership(edgeId: edgeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: graphContext.Storage!.Definition!, parameterName: nameof(edgeId));
        ValidateGraphPayloadUpdate(update: update, payloadColumnCount: graphContext.Storage!.Definition!.Columns.Count - 3);

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        var rowId = await GraphEdgeFindAsync(graphContext: graphContext, edgeId: edgeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (rowId is null)
        {
            return false;
        }

        var result = await UpdateGraphRowAsync(tableStorageContext: graphContext.Storage!, rowId: rowId.Value, update: update, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (result.Updated)
        {
            await PublishGraphAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return result.Updated;
    }

    private ValueTask<bool> GraphEdgeReconnectAsync(StorageGraphContext graphContext, GraphEdgeId edgeId, GraphNodeId fromNodeId, GraphNodeId toNodeId, CancellationToken cancellationToken = default) =>
        GraphEdgeUpdateAndReconnectAsync(graphContext: graphContext, edgeId: edgeId, fromNodeId: fromNodeId, toNodeId: toNodeId, update: new RowUpdate { Columns = Array.AsReadOnly(array: Array.Empty<ColumnUpdate>()) }, cancellationToken: cancellationToken);

    private async ValueTask<bool> GraphEdgeUpdateAndReconnectAsync(StorageGraphContext graphContext, GraphEdgeId edgeId, GraphNodeId fromNodeId, GraphNodeId toNodeId, RowUpdate update, CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);
        ValidateGraphEdgeOwnership(edgeId: edgeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: graphContext.Storage!.Definition!, parameterName: nameof(edgeId));
        ValidateGraphPayloadUpdate(update: update, payloadColumnCount: graphContext.Storage!.Definition!.Columns.Count - 3);

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        await GraphEdgeEnsureNodeExistsAsync(graphContext: graphContext, nodeId: fromNodeId, expectedTableId: graphContext.Storage!.Definition!.Graph!.FromNodeTableId!.Value, parameterName: nameof(fromNodeId), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await GraphEdgeEnsureNodeExistsAsync(graphContext: graphContext, nodeId: toNodeId, expectedTableId: graphContext.Storage!.Definition!.Graph!.ToNodeTableId!.Value, parameterName: nameof(toNodeId), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var rowId = await GraphEdgeFindAsync(graphContext: graphContext, edgeId: edgeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (rowId is null)
        {
            return false;
        }

        var result = await UpdateGraphRowAsync(tableStorageContext: graphContext.Storage!, rowId: rowId.Value, update: new RowUpdate { Columns = Array.AsReadOnly(array: (ColumnUpdate[])[..update.Columns, new ColumnUpdate { ColumnIndex = graphContext.Storage!.Definition!.Columns.Count - 2, Value = EncodeGraphNodeId(graphNodeId: fromNodeId) }, new ColumnUpdate { ColumnIndex = graphContext.Storage!.Definition!.Columns.Count - 1, Value = EncodeGraphNodeId(graphNodeId: toNodeId) }]) }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (result.Updated)
        {
            await PublishGraphAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return result.Updated;
    }

    private async ValueTask<bool> GraphEdgeDeleteAsync(StorageGraphContext graphContext, GraphEdgeId edgeId, CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);
        ValidateGraphEdgeOwnership(edgeId: edgeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: graphContext.Storage!.Definition!, parameterName: nameof(edgeId));

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        var rowId = await GraphEdgeFindAsync(graphContext: graphContext, edgeId: edgeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (rowId is null)
        {
            return false;
        }

        var result = await LowPublicTableDeleteAsync(tableStorageContext: graphContext.Storage!, rowId: rowId.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (result.Deleted)
        {
            await PublishGraphAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return result.Deleted;
    }

    private async IAsyncEnumerable<StoredGraphEdge> GraphEdgeTraverseAsyncCore(StorageGraphContext graphContext, GraphNodeId nodeId, GraphEdgeDirection direction, GraphTraversalOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);

        if (!Enum.IsDefined(value: direction))
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        options ??= new GraphTraversalOptions
        {
            MaximumEdges = 10_000
        };

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        var outgoing = graphContext.Storage!.Definition!.Graph!.FromNodeTableId == nodeId.TableId;
        var incoming = graphContext.Storage!.Definition!.Graph!.ToNodeTableId == nodeId.TableId;

        if (direction == GraphEdgeDirection.Outgoing && !outgoing || direction == GraphEdgeDirection.Incoming && !incoming || direction == GraphEdgeDirection.Both && !outgoing && !incoming)
        {
            throw new ArgumentException("The graph node identity is incompatible with the requested edge direction.", nameof(nodeId));
        }

        var nodeValidated = false;

        if (outgoing && (direction is GraphEdgeDirection.Outgoing or GraphEdgeDirection.Both))
        {
            await GraphEdgeEnsureNodeExistsAsync(graphContext: graphContext, nodeId: nodeId, expectedTableId: graphContext.Storage!.Definition!.Graph!.FromNodeTableId!.Value, parameterName: nameof(nodeId), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            nodeValidated = true;
        }

        if (incoming && (direction is GraphEdgeDirection.Incoming or GraphEdgeDirection.Both) && !nodeValidated)
        {
            await GraphEdgeEnsureNodeExistsAsync(graphContext: graphContext, nodeId: nodeId, expectedTableId: graphContext.Storage!.Definition!.Graph!.ToNodeTableId!.Value, parameterName: nameof(nodeId), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        HashSet<RowId> rows = [];

        if ((direction is GraphEdgeDirection.Outgoing or GraphEdgeDirection.Both) && outgoing)
        {
            await GraphEdgeAddMatchesAsync(graphContext: graphContext, indexId: graphContext.Storage!.Definition!.Graph!.OutgoingIndexId!.Value, nodeId: nodeId, rows: rows, maximumEdges: options.MaximumEdges, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        if ((direction is GraphEdgeDirection.Incoming or GraphEdgeDirection.Both) && incoming)
        {
            await GraphEdgeAddMatchesAsync(graphContext: graphContext, indexId: graphContext.Storage!.Definition!.Graph!.IncomingIndexId!.Value, nodeId: nodeId, rows: rows, maximumEdges: options.MaximumEdges, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        foreach (var rowId in rows.OrderBy(keySelector: row => row, comparer: Comparer<RowId>.Create(comparison: CompareGraphRowIds)))
        {
            EnsureGraphCurrent(graphContext: graphContext);
            cancellationToken.ThrowIfCancellationRequested();

            yield return await GraphEdgeReadAsync(graphContext: graphContext, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private ValueTask<RowId?> GraphEdgeFindAsync(StorageGraphContext graphContext, GraphEdgeId edgeId, CancellationToken cancellationToken) =>
        FindGraphUniqueAsync(graphContext: graphContext, table: graphContext.Storage!.Definition!, indexId: graphContext.Storage!.Definition!.Graph!.IdentityIndexId, identity: EncodeGraphEdgeId(graphEdgeId: edgeId), cancellationToken: cancellationToken);

    private async ValueTask GraphEdgeEnsureNodeExistsAsync(StorageGraphContext graphContext, GraphNodeId nodeId, TableId expectedTableId, string parameterName, CancellationToken cancellationToken)
    {
        var table = catalog.Definition.Tables.Single(predicate: table => table.Id == expectedTableId);
        ValidateGraphNodeOwnership(nodeId: nodeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: table, parameterName: parameterName);

        var rowId = await FindGraphUniqueAsync(graphContext: graphContext, table: table, indexId: table.Graph!.IdentityIndexId, identity: EncodeGraphNodeId(graphNodeId: nodeId), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (rowId is null)
        {
            throw new GraphReferentialIntegrityException("A graph edge endpoint does not reference a live node.");
        }
    }

    private async ValueTask GraphEdgeAddMatchesAsync(StorageGraphContext graphContext, IndexId indexId, GraphNodeId nodeId, HashSet<RowId> rows, int maximumEdges, CancellationToken cancellationToken)
    {
        var index = catalog.Definition.Indexes.Single(predicate: candidate => candidate.Id == indexId);
        var range = ReadGraphExactRange(table: graphContext.Storage!.Definition!, index: index, value: EncodeGraphNodeId(graphNodeId: nodeId));

        await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in ScanGraphIndexEntries(graphContext: graphContext, index: index, range: range, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (rows.Contains(item: entry.RowId))
            {
                continue;
            }

            if (rows.Count == maximumEdges)
            {
                throw new StorageResourceExhaustedException($"Graph adjacency exceeds the configured {maximumEdges}-edge traversal limit.");
            }

            rows.Add(item: entry.RowId);
        }
    }

    private async ValueTask<StoredGraphEdge> GraphEdgeReadAsync(StorageGraphContext graphContext, RowId rowId, CancellationToken cancellationToken)
    {
        var found = await ReadPublicTableRowAsync(tableStorageContext: graphContext.Storage!, rowId: rowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!found.Found)
        {
            throw new StorageCorruptionException("A graph index references a missing edge row.");
        }

        var row = found.Row!;

        try
        {
            var edgeId = DecodeGraphEdgeId(sqlValue: row.Values[^3]);
            var fromNodeId = DecodeGraphNodeId(sqlValue: row.Values[^2]);
            var toNodeId = DecodeGraphNodeId(sqlValue: row.Values[^1]);
            ValidateGraphEdgeOwnership(edgeId: edgeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: graphContext.Storage!.Definition!, parameterName: nameof(edgeId));
            ValidateGraphNodeOwnership(nodeId: fromNodeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: catalog.Definition.Tables.Single(predicate: table => table.Id == graphContext.Storage!.Definition!.Graph!.FromNodeTableId), parameterName: nameof(fromNodeId));
            ValidateGraphNodeOwnership(nodeId: toNodeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: catalog.Definition.Tables.Single(predicate: table => table.Id == graphContext.Storage!.Definition!.Graph!.ToNodeTableId), parameterName: nameof(toNodeId));

            return new StoredGraphEdge
            {
                EdgeId = edgeId,
                FromNodeId = fromNodeId,
                ToNodeId = toNodeId,
                Row = ReadGraphPayload(stored: row, generatedCount: 3)
            };
        }
        catch (ArgumentException exception)
        {
            throw new StorageCorruptionException("A stored graph edge identity has invalid ownership metadata.", exception);
        }
    }
}