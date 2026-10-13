// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    public ValueTask<StorageSessionContext> ExecuteStorageSessionContextGraphAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateGraphRequest(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var graph = storageSessionContext.Graph!;

        if (graph.Endpoints is not null)
        {
            graph = graph with
            {
                FromNodeId = graph.Endpoints.FromNodeId,
                ToNodeId = graph.Endpoints.ToNodeId
            };

            storageSessionContext.Graph = graph;
        }

        if (graph.TableEndpoints is not null)
        {
            graph = graph with
            {
                FromTableId = graph.TableEndpoints.FromNodeTableId,
                ToTableId = graph.TableEndpoints.ToNodeTableId
            };

            storageSessionContext.Graph = graph;
        }

        switch (graph.Operation)
        {
            case "NodeInsert":
                graph.NodeId = await GraphNodeInsertAsync(graphContext: graph, row: graph.Row!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "NodeGet":
                graph.Node = await GraphNodeGetAsync(graphContext: graph, nodeId: graph.NodeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "NodeUpdate":
                graph.Result = await GraphNodeUpdateAsync(graphContext: graph, nodeId: graph.NodeId, update: graph.Update!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "NodeDelete":
                graph.Result = await GraphNodeDeleteAsync(graphContext: graph, nodeId: graph.NodeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "EdgeInsert":
                graph.EdgeId = await GraphEdgeInsertAsync(graphContext: graph, fromNodeId: graph.FromNodeId, toNodeId: graph.ToNodeId, row: graph.Row!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "EdgeGet":
                graph.Edge = await GraphEdgeGetAsync(graphContext: graph, edgeId: graph.EdgeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "EdgeUpdate":
                graph.Result = await GraphEdgeUpdateAsync(graphContext: graph, edgeId: graph.EdgeId, update: graph.Update!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "EdgeReconnect":
                graph.Result = await GraphEdgeReconnectAsync(graphContext: graph, edgeId: graph.EdgeId, fromNodeId: graph.FromNodeId, toNodeId: graph.ToNodeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "EdgeUpdateReconnect":
                graph.Result = await GraphEdgeUpdateAndReconnectAsync(graphContext: graph, edgeId: graph.EdgeId, fromNodeId: graph.FromNodeId, toNodeId: graph.ToNodeId, update: graph.Update!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "EdgeDelete":
                graph.Result = await GraphEdgeDeleteAsync(graphContext: graph, edgeId: graph.EdgeId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            case "Create":
                graph.CreatedTable = await BuildPublicGraphTableAsync(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                break;
            default:
                throw new ArgumentException(message: "Unsupported graph operation.");
        }

        return storageSessionContext;
    });

    public StorageSessionContext ReadStorageSessionContextGraphStream(StorageSessionContext storageSessionContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateGraphRequest(storageSessionContext: storageSessionContext, cancellationToken: cancellationToken);
        var graph = storageSessionContext.Graph!;

        if (graph.Endpoints is not null)
        {
            graph = graph with
            {
                FromNodeId = graph.Endpoints.FromNodeId,
                ToNodeId = graph.Endpoints.ToNodeId
            };

            storageSessionContext.Graph = graph;
        }

        if (graph.TableEndpoints is not null)
        {
            graph = graph with
            {
                FromTableId = graph.TableEndpoints.FromNodeTableId,
                ToTableId = graph.TableEndpoints.ToNodeTableId
            };

            storageSessionContext.Graph = graph;
        }

        if (graph.Operation == "NodeScan")
        {
            graph.Nodes = TrackPublicIndexStream(source: GraphNodeScanAsyncCore(graphContext: graph, cancellationToken: cancellationToken), storageIndexContext: new StorageIndexContext { Handle = graph.Handle });
        }
        else if (graph.Operation == "EdgeTraverse")
        {
            graph.Edges = TrackPublicIndexStream(source: GraphEdgeTraverseAsyncCore(graphContext: graph, nodeId: graph.NodeId, direction: graph.Direction, options: graph.Options, cancellationToken: cancellationToken), storageIndexContext: new StorageIndexContext { Handle = graph.Handle });
        }
        else
        {
            throw new ArgumentException(message: "Unsupported graph stream operation.");
        }

        return storageSessionContext;
    });
}