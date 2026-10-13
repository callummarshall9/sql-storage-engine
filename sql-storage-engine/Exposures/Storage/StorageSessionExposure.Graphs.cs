// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure : IStorageGraphNodeTable, IStorageGraphEdgeTable
{
    CatalogTable IStorageGraphNodeTable.ReadDefinition() =>
        sessionContext.Graph!.Storage!.Definition!;

    CatalogTable IStorageGraphEdgeTable.ReadDefinition() =>
        sessionContext.Graph!.Storage!.Definition!;

    CatalogGraphTable IStorageGraphNodeTable.ReadGraphDefinition() =>
        sessionContext.Graph!.Storage!.Definition!.Graph!;

    CatalogGraphTable IStorageGraphEdgeTable.ReadGraphDefinition() =>
        sessionContext.Graph!.Storage!.Definition!.Graph!;

    async ValueTask<GraphNodeId> IStorageGraphNodeTable.InsertAsync(Row newRow, CancellationToken cancellationToken) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "NodeInsert", Row = newRow } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.NodeId;

    public async ValueTask<StoredGraphNode?> GetAsync(GraphNodeId graphNodeId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "NodeGet", NodeId = graphNodeId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.Node;

    public async ValueTask<bool> UpdateAsync(GraphNodeId updatedGraphNodeId, RowUpdate updatedRowUpdate, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "NodeUpdate", NodeId = updatedGraphNodeId, Update = updatedRowUpdate } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.Result;

    public async ValueTask<bool> DeleteAsync(GraphNodeId deletedGraphNodeId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "NodeDelete", NodeId = deletedGraphNodeId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.Result;

    public async ValueTask<GraphEdgeId> InsertAsync(GraphEndpoints newGraphEndpoints, Row newRow, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "EdgeInsert", Endpoints = newGraphEndpoints, Row = newRow } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.EdgeId;

    public async ValueTask<StoredGraphEdge?> GetAsync(GraphEdgeId graphEdgeId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "EdgeGet", EdgeId = graphEdgeId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.Edge;

    public async ValueTask<bool> UpdateAsync(GraphEdgeId updatedGraphEdgeId, RowUpdate updatedRowUpdate, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "EdgeUpdate", EdgeId = updatedGraphEdgeId, Update = updatedRowUpdate } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.Result;

    public async ValueTask<bool> ReconnectAsync(GraphEdgeId graphEdgeId, GraphEndpoints graphEndpoints, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "EdgeReconnect", EdgeId = graphEdgeId, Endpoints = graphEndpoints } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.Result;

    public async ValueTask<bool> UpdateAndReconnectAsync(GraphEdgeId updatedGraphEdgeId, GraphEndpoints updatedGraphEndpoints, RowUpdate updatedRowUpdate, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "EdgeUpdateReconnect", EdgeId = updatedGraphEdgeId, Endpoints = updatedGraphEndpoints, Update = updatedRowUpdate } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.Result;

    public async ValueTask<bool> DeleteAsync(GraphEdgeId deletedGraphEdgeId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "EdgeDelete", EdgeId = deletedGraphEdgeId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.Result;

    IAsyncEnumerable<StoredGraphNode> IStorageGraphNodeTable.ScanAsync(CancellationToken cancellationToken) =>
        service.ReadStorageSessionContextGraphStream(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "NodeScan" } }, cancellationToken: cancellationToken)
            .Graph!.Nodes!;

    public IAsyncEnumerable<StoredGraphEdge> TraverseAsync(GraphNodeId graphNodeId, GraphEdgeDirection graphEdgeDirection, GraphTraversalOptions? graphTraversalOptions = null, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextGraphStream(storageSessionContext: new StorageSessionContext { Graph = sessionContext.Graph!with { Operation = "EdgeTraverse", NodeId = graphNodeId, Direction = graphEdgeDirection, Options = graphTraversalOptions } }, cancellationToken: cancellationToken)
            .Graph!.Edges!;

    public async ValueTask<IStorageGraphNodeTable> OpenGraphNodeTableAsync(TableId tableId, CancellationToken cancellationToken = default) =>
        new StorageSessionExposure(storageSessionContext: (await service.ReadStorageSessionContextEngineHandleAsync(storageSessionContext: new StorageSessionContext { OpenHandle = new StorageEngineOpenHandleContext { GraphKind = 1, TableId = tableId, Handle = new StorageHandleContext { Engine = engineContext, Statement = sessionContext.Statement, Generation = engineContext.Generation } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).OpenHandle!.Result!);

    public async ValueTask<IStorageGraphEdgeTable> OpenGraphEdgeTableAsync(TableId tableId, CancellationToken cancellationToken = default) =>
        new StorageSessionExposure(storageSessionContext: (await service.ReadStorageSessionContextEngineHandleAsync(storageSessionContext: new StorageSessionContext { OpenHandle = new StorageEngineOpenHandleContext { GraphKind = 2, TableId = tableId, Handle = new StorageHandleContext { Engine = engineContext, Statement = sessionContext.Statement, Generation = engineContext.Generation } } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).OpenHandle!.Result!);

    public async ValueTask<CatalogTable> CreateCatalogTableGraphNodeAsync(CatalogTableName catalogTableName, IEnumerable<CatalogColumn> payloadColumns, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = new StorageGraphContext { Operation = "Create", Kind = 1, Handle = new StorageHandleContext { Engine = engineContext, Statement = sessionContext.Statement, Generation = engineContext.Generation }, Name = catalogTableName, PayloadColumns = payloadColumns } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.CreatedTable!;

    public async ValueTask<CatalogTable> CreateCatalogTableGraphEdgeAsync(CatalogTableName catalogTableName, IEnumerable<CatalogColumn> payloadColumns, GraphTableEndpoints graphTableEndpoints, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextGraphAsync(storageSessionContext: new StorageSessionContext { Graph = new StorageGraphContext { Operation = "Create", Handle = new StorageHandleContext { Engine = engineContext, Statement = sessionContext.Statement, Generation = engineContext.Generation }, Name = catalogTableName, PayloadColumns = payloadColumns, Kind = 2, TableEndpoints = graphTableEndpoints } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Graph!.CreatedTable!;
}