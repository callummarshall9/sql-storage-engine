// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private async ValueTask<GraphNodeId> GraphNodeInsertAsync(StorageGraphContext graphContext, Row row, CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);
        ValidateGraphPayload(row: row, table: graphContext.Storage!.Definition!, generatedCount: 1, parameterName: nameof(row));

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        var nodeId = CreateGraphNodeId(databaseId: ReadGraphDatabaseId(graphContext: graphContext), tableId: graphContext.Storage!.Definition!.Id, schemaVersion: graphContext.Storage!.Definition!.SchemaVersion, value: CreateGuidGraphIdentifier());

        var result = await InsertGraphRowAsync(tableStorageContext: graphContext.Storage!, row: AppendGraphPayload(payload: row, generated: EncodeGraphNodeId(graphNodeId: nodeId)), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!result.Inserted)
        {
            throw new DuplicateKeyIgnoredException(result.Warnings.Single());
        }

        await PublishGraphAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return nodeId;
    }

    private async ValueTask<StoredGraphNode?> GraphNodeGetAsync(StorageGraphContext graphContext, GraphNodeId nodeId, CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);
        ValidateGraphNodeOwnership(nodeId: nodeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: graphContext.Storage!.Definition!, parameterName: nameof(nodeId));

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        var rowId = await GraphNodeFindAsync(graphContext: graphContext, nodeId: nodeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (rowId is null)
        {
            return null;
        }

        var found = await ReadPublicTableRowAsync(tableStorageContext: graphContext.Storage!, rowId: rowId.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!found.Found)
        {
            throw new StorageCorruptionException("A graph identity index references a missing node row.");
        }

        return GraphNodeToNode(graphContext: graphContext, row: found.Row!);
    }

    private async IAsyncEnumerable<StoredGraphNode> GraphNodeScanAsyncCore(StorageGraphContext graphContext, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        await foreach (var row in LowPublicTableScanAsync(tableStorageContext: graphContext.Storage!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            EnsureGraphCurrent(graphContext: graphContext);
            cancellationToken.ThrowIfCancellationRequested();
            yield return GraphNodeToNode(graphContext: graphContext, row: row.Row);
        }
    }

    private async ValueTask<bool> GraphNodeUpdateAsync(StorageGraphContext graphContext, GraphNodeId nodeId, RowUpdate update, CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);
        ValidateGraphNodeOwnership(nodeId: nodeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: graphContext.Storage!.Definition!, parameterName: nameof(nodeId));
        ValidateGraphPayloadUpdate(update: update, payloadColumnCount: graphContext.Storage!.Definition!.Columns.Count - 1);

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        var rowId = await GraphNodeFindAsync(graphContext: graphContext, nodeId: nodeId, cancellationToken: cancellationToken)
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

    private async ValueTask<bool> GraphNodeDeleteAsync(StorageGraphContext graphContext, GraphNodeId nodeId, CancellationToken cancellationToken = default)
    {
        EnsureGraphCurrent(graphContext: graphContext);
        ValidateGraphNodeOwnership(nodeId: nodeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: graphContext.Storage!.Definition!, parameterName: nameof(nodeId));

        var lease = await EnterGraphScopeAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        using var gateLifetime = lease.Lifetime!;

        using var accessScope = engineResources.ActivateEngineResourceContext(engineResourceContext: lease)
            .Lifetime!;

        var rowId = await GraphNodeFindAsync(graphContext: graphContext, nodeId: nodeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (rowId is null)
        {
            return false;
        }

        await GraphNodeEnsureUnreferencedAsync(graphContext: graphContext, nodeId: nodeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var result = await LowPublicTableDeleteAsync(tableStorageContext: graphContext.Storage!, rowId: rowId.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (result.Deleted)
        {
            await PublishGraphAsync(graphContext: graphContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return result.Deleted;
    }

    private ValueTask<Models.Identifiers.RowId?> GraphNodeFindAsync(StorageGraphContext graphContext, GraphNodeId nodeId, CancellationToken cancellationToken) =>
        FindGraphUniqueAsync(graphContext: graphContext, table: graphContext.Storage!.Definition!, indexId: graphContext.Storage!.Definition!.Graph!.IdentityIndexId, identity: EncodeGraphNodeId(graphNodeId: nodeId), cancellationToken: cancellationToken);

    private async ValueTask GraphNodeEnsureUnreferencedAsync(StorageGraphContext graphContext, GraphNodeId nodeId, CancellationToken cancellationToken)
    {
        foreach (var edgeTable in catalog.Definition.Tables.Where(predicate: table => table.Graph?.Kind == GraphTableKind.Edge))
        {
            var graph = edgeTable.Graph!;

            foreach (var indexId in GraphNodeEndpointIndexes(graphContext: graphContext, graph: graph, nodeTableId: nodeId.TableId))
            {
                var index = catalog.Definition.Indexes.Single(predicate: candidate => candidate.Id == indexId);
                var range = ReadGraphExactRange(table: edgeTable, index: index, value: EncodeGraphNodeId(graphNodeId: nodeId));

                await using var rows = ScanGraphIndexEntries(graphContext: graphContext, index: index, range: range, cancellationToken: cancellationToken)
            .GetAsyncEnumerator(cancellationToken: cancellationToken);

                if (await rows.MoveNextAsync()
            .ConfigureAwait(continueOnCapturedContext: false))
                {
                    throw new GraphReferentialIntegrityException("A graph node cannot be deleted while one or more edges reference it.");
                }
            }
        }
    }

    private static IEnumerable<Models.Identifiers.IndexId> GraphNodeEndpointIndexes(StorageGraphContext graphContext, CatalogGraphTable graph, Models.Identifiers.TableId nodeTableId)
    {
        if (graph.FromNodeTableId == nodeTableId)
        {
            yield return graph.OutgoingIndexId!.Value;
        }

        if (graph.ToNodeTableId == nodeTableId && graph.IncomingIndexId != graph.OutgoingIndexId)
        {
            yield return graph.IncomingIndexId!.Value;
        }
    }

    private StoredGraphNode GraphNodeToNode(StorageGraphContext graphContext, Row row)
    {
        try
        {
            var nodeId = DecodeGraphNodeId(sqlValue: row.Values[^1]);
            ValidateGraphNodeOwnership(nodeId: nodeId, databaseId: ReadGraphDatabaseId(graphContext: graphContext), table: graphContext.Storage!.Definition!, parameterName: nameof(nodeId));

            return new StoredGraphNode
            {
                NodeId = nodeId,
                Row = ReadGraphPayload(stored: row, generatedCount: 1)
            };
        }
        catch (ArgumentException exception)
        {
            throw new StorageCorruptionException("A stored graph node identity has invalid ownership metadata.", exception);
        }
    }
}