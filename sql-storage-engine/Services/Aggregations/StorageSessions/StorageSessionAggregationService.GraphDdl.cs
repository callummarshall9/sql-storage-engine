// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Graph;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Aggregations.StorageSessions;
internal sealed partial class StorageSessionAggregationService
{
    private async ValueTask<CatalogTable> BuildPublicGraphTableAsync(StorageSessionContext storageSessionContext, CancellationToken cancellationToken)
    {
        var graph = storageSessionContext.Graph!;
        EnsureGraphCurrent(graphContext: graph);

        if (graph.Handle!.Statement is null)
        {
            CatalogTable? result = null;

            await ExecutePublicStatementCoreAsync(storageEngineRuntime: graph.Handle.Engine!, operation: async (context, token) =>
            {
                result = await BuildPublicGraphTableCoreAsync(graph: graph with { Handle = new StorageHandleContext { Engine = context.Engine, Statement = context.Statement, Generation = context.Engine!.Generation } }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);
            }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return result!;
        }

        try
        {
            return await BuildPublicGraphTableCoreAsync(graph: graph, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch
        {
            graph.Handle.Statement.DdlFailed = true;
            throw;
        }
    }

    private async ValueTask<CatalogTable> BuildPublicGraphTableCoreAsync(StorageGraphContext graph, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = graph.PayloadColumns!.ToArray();

        if (payload.Any(predicate: column => column is null || column.IsHidden || column.GeneratedAlways != CatalogGeneratedAlwaysKind.None || column.Name.StartsWith(value: "$", comparisonType: StringComparison.Ordinal)))
        {
            throw new ArgumentException(message: "Graph payload cannot supply generated or reserved columns.", paramName: nameof(graph.PayloadColumns));
        }

        var edge = graph.FromTableId.HasValue;

        if (edge != graph.ToTableId.HasValue)
        {
            throw new ArgumentException(message: "Both endpoint tables are required.");
        }

        if (edge)
        {
            ValidatePublicGraphEndpoint(tableId: graph.FromTableId!.Value);
            ValidatePublicGraphEndpoint(tableId: graph.ToTableId!.Value);
        }

        var last = payload.Length == 0 ? 0UL : payload.Max(selector: column => column.Id.Value);

        var identity = new ColumnId
        {
            Value = checked(last + 1)
        };

        var generated = new List<CatalogColumn>
        {
            CreateCatalogColumnGraphGenerated(columnId: identity, name: edge ? "$edge_id" : "$node_id", kind: CatalogGeneratedAlwaysKind.GraphIdentity)
        };

        if (edge)
        {
            generated.Add(item: CreateCatalogColumnGraphGenerated(columnId: new ColumnId { Value = checked(last + 2) }, name: "$from_id", kind: CatalogGeneratedAlwaysKind.GraphFromNode));
            generated.Add(item: CreateCatalogColumnGraphGenerated(columnId: new ColumnId { Value = checked(last + 3) }, name: "$to_id", kind: CatalogGeneratedAlwaysKind.GraphToNode));
        }

        var resources = engineResources.ReadEngineResourceContext(engineResourceContext: (EngineResourceContext)graph.Handle!.Engine!.ResourceSource!);

        var table = await CreateCatalogTableAsync(name: graph.Name!, schemaVersion: 1, columns: ApplyEngineDatabaseCollation(columns: payload.Concat(second: generated), defaultCollation: resources.Header!.DefaultCollation), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await PublishPublicGraphDdlStageAsync(graph: graph, stage: GraphDdlStage.TableCreated, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var identityIndex = await CreateCatalogIndexAsync(name: "$graph_identity", tableId: table.Id, isUnique: true, columns: [CreateCatalogIndexedColumnGraph(columnId: identity)], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await PublishPublicGraphDdlStageAsync(graph: graph, stage: GraphDdlStage.IdentityIndexCreated, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        CatalogTable result;

        if (!edge)
        {
            result = await RegisterGraphNodeTableAsync(tableId: table.Id, identityColumnId: identity, identityIndexId: identityIndex.Id, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        else
        {
            var outgoing = await CreateCatalogIndexAsync(name: "$graph_outgoing", tableId: table.Id, isUnique: false, columns: [CreateCatalogIndexedColumnGraph(columnId: generated[1].Id)], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await PublishPublicGraphDdlStageAsync(graph: graph, stage: GraphDdlStage.OutgoingIndexCreated, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var incoming = await CreateCatalogIndexAsync(name: "$graph_incoming", tableId: table.Id, isUnique: false, columns: [CreateCatalogIndexedColumnGraph(columnId: generated[2].Id)], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await PublishPublicGraphDdlStageAsync(graph: graph, stage: GraphDdlStage.IncomingIndexCreated, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            result = await RegisterGraphEdgeTableAsync(tableId: table.Id, identityColumnId: identity, identityIndexId: identityIndex.Id, fromNodeTableId: graph.FromTableId!.Value, fromNodeColumnId: generated[1].Id, outgoingIndexId: outgoing.Id, toNodeTableId: graph.ToTableId!.Value, toNodeColumnId: generated[2].Id, incomingIndexId: incoming.Id, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        graph.Handle!.Engine!.Generation = checked(graph.Handle!.Engine!.Generation + 1);

        await PublishPublicGraphDdlStageAsync(graph: graph, stage: GraphDdlStage.Registered, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return result;
    }

    private void ValidatePublicGraphEndpoint(TableId tableId)
    {
        if (!TryOpenTable(tableId: tableId, table: out var table) || table!.Graph?.Kind != GraphTableKind.Node)
        {
            throw new ArgumentException(message: "A graph endpoint must reference a registered node table.");
        }
    }

    private async ValueTask PublishPublicGraphDdlStageAsync(StorageGraphContext graph, GraphDdlStage stage, CancellationToken cancellationToken)
    {
        await FlushPublicEngineAsync(storageEngineRuntime: graph.Handle!.Engine!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        graph.Handle!.Engine!.GraphDdlObserver?.Invoke(obj: stage);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private CatalogColumn CreateCatalogColumnGraphGenerated(ColumnId columnId, string name, CatalogGeneratedAlwaysKind kind) =>
        CreateCatalogColumn(columnId: columnId, name: name, sqlType: new SqlType { Name = SqlTypeName.Binary, Length = 50 }, isNullable: false, generatedAlways: kind, isHidden: true);

    private static CatalogIndexedColumn CreateCatalogIndexedColumnGraph(ColumnId columnId) =>
        new CatalogIndexedColumn
    {
        ColumnId = columnId,
        Direction = SortDirection.Ascending,
        NullSortOrder = NullSortOrder.First
    };
}