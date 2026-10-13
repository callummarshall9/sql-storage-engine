// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Exposures.Identifiers;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.UnitTests;
public sealed class GraphStorageTests
{
    [Test]
    public void TypedIdentitiesRoundTripAndRejectWrongKindsAndMalformedValues()
    {
        var databaseId = DatabaseIdExposure.CreateDatabaseId();
        var node = sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.CreateGraphNodeId(databaseId:databaseId,tableId:new sql_storage_engine.Models.Identifiers.TableId { Value = 2 },schemaVersion:3,value:Guid.NewGuid());
        var edge = sql_storage_engine.Exposures.Graph.GraphEdgeIdExposure.CreateGraphEdgeId(databaseId:databaseId,tableId:new sql_storage_engine.Models.Identifiers.TableId { Value = 4 },schemaVersion:5,value:Guid.NewGuid());
sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.FromSqlValue(sqlValue:sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.ToSqlValue(graphNodeId:node)).Should().Be(node);
sql_storage_engine.Exposures.Graph.GraphEdgeIdExposure.FromSqlValue(sqlValue:sql_storage_engine.Exposures.Graph.GraphEdgeIdExposure.ToSqlValue(graphEdgeId:edge)).Should().Be(edge);
        ((Func<GraphNodeId>)(() => sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.FromSqlValue(sqlValue:sql_storage_engine.Exposures.Graph.GraphEdgeIdExposure.ToSqlValue(graphEdgeId:edge)))).Should().Throw<ArgumentException>();
        ((Func<GraphNodeId>)(() => sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.FromSqlValue(sqlValue:sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue([1, 2])))).Should().Throw<ArgumentException>();
        ((Func<GraphTraversalOptions>)(() => sql_storage_engine.Exposures.Graph.GraphTraversalOptionsExposure.CreateGraphTraversalOptions(0))).Should().Throw<ArgumentOutOfRangeException>();
        ((Func<GraphTraversalOptions>)(() => sql_storage_engine.Exposures.Graph.GraphTraversalOptionsExposure.CreateGraphTraversalOptions(GraphTraversalOptions.MaximumSupportedEdges + 1))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task GraphRegistrationPersistsAndOrdinaryMutationCannotBypassGeneratedIdentity()
    {
        var path = TempPath("graph-catalog");
        try
        {
            TableId nodeTableId;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var nodeTable = await CreateNodeTableAsync(engine,"nodes");
                nodeTableId = nodeTable.Id;
                var ordinary = await engine.OpenTableAsync(nodeTable.Id);
                var identity = engine.ReadCatalog().GetIndexes(nodeTable.Id).Single(index => index.Name == "nodes_graph_id");
                var registered = await engine.RegisterGraphNodeTableAsync(nodeTable.Id, new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, identity.Id);
                registered.SchemaVersion.Should().Be(2);
                registered.Graph!.Kind.Should().Be(GraphTableKind.Node);
                await ((Func<Task>)(async () => await ordinary.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("stale"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.EncodedLength])])))).Should().ThrowAsync<InvalidOperationException>();
                var currentOrdinary = await engine.OpenTableAsync(nodeTable.Id);
                await ((Func<Task>)(async () => await currentOrdinary.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("forged"), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateBinarySqlValue(new byte[sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.EncodedLength])])))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*graph storage handle*");
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var persisted = reopened.ReadCatalog().ReadTables().Single(table => table.Id == nodeTableId);
            persisted.Graph!.Kind.Should().Be(GraphTableKind.Node);
            persisted.Graph.IdentityColumnId.Should().Be(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 });
            (await reopened.OpenGraphNodeTableAsync(nodeTableId)).ReadDefinition().SchemaVersion.Should().Be(2);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task RegistrationRejectsMalformedMetadataAndNonemptyTablesWithoutPublishingGraphScope()
    {
        var path = TempPath("graph-registration-failure");
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var wrong = await engine.CreateCatalogTableAsync("wrong",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), GraphColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"$node_id",CatalogGeneratedAlwaysKind.GraphIdentity)]);
            var nonUnique = await engine.CreateCatalogIndexAsync("wrong_graph_id",wrong.Id,false,[Indexed(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 })]);
            await ((Func<Task>)(async () => await engine.RegisterGraphNodeTableAsync(wrong.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },nonUnique.Id))).Should().ThrowAsync<ArgumentException>();
            engine.ReadCatalog().ReadTables().Single(table => table.Id == wrong.Id).Graph.Should().BeNull();
            var populated = await engine.CreateCatalogTableAsync("populated",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)]);
            var populatedRows = await engine.OpenTableAsync(populated.Id);
            await populatedRows.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]));
            var populatedIndex = await engine.CreateCatalogIndexAsync("populated_id",populated.Id,true,[Indexed(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 })]);
            await ((Func<Task>)(async () => await engine.RegisterGraphNodeTableAsync(populated.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },populatedIndex.Id))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*must be empty*");
            engine.ReadCatalog().ReadTables().Single(table => table.Id == populated.Id).Graph.Should().BeNull();
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task GraphInsertPreservesOrdinaryPayloadGeneration()
    {
        var path = TempPath("graph-generated-payload");
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var table = await engine.CreateCatalogTableAsync("generated_nodes",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, catalogIdentity: sql_storage_engine.Exposures.Catalog.CatalogIdentityExposure.CreateCatalogIdentity(10,2)), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "quantity", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, defaultExpression: "2"), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "total", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "[quantity] * 3", isComputedPersisted: true), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 }, "rv", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeRowVersion(), false), GraphColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 5 },"$node_id",CatalogGeneratedAlwaysKind.GraphIdentity)]);
            var identity = await engine.CreateCatalogIndexAsync("generated_nodes_graph_id",table.Id,true,[Indexed(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 5 })]);
            await engine.CreateCatalogIndexAsync("generated_nodes_quantity",table.Id,true,[Indexed(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 })],sql_storage_engine.Exposures.Catalog.CatalogBTreeIndexOptionsExposure.CreateCatalogBTreeIndexOptions(ignoreDuplicateKey: true));
            await engine.RegisterGraphNodeTableAsync(table.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 5 },identity.Id);
            var nodes = await engine.OpenGraphNodeTableAsync(table.Id);
            var nodeId = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()]));
            var stored = (await nodes.GetAsync(nodeId))!.Row;
            stored.Values[0].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(10));
            stored.Values[1].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2));
            stored.Values[2].Should().Be(sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(6));
            stored.Values[3].Should().BeOfType<BinarySqlValue>();
            await ((Func<Task>)(async () => await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()])))).Should().ThrowAsync<DuplicateKeyIgnoredException>();
            var count = 0;
            await foreach (var _ in nodes.ScanAsync())
                count++;
            count.Should().Be(1);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task EndpointSafeMutationsTraversalLimitsCancellationAndReopenRemainConsistent()
    {
        var path = TempPath("graph-runtime");
        GraphNodeId first;
        GraphNodeId second;
        GraphNodeId third;
        GraphEdgeId firstEdge;
        GraphEdgeId secondEdge;
        TableId nodeTableId;
        TableId edgeTableId;
        try
        {
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var nodeDefinition = await RegisterNodeAsync(engine,"people");
                nodeTableId = nodeDefinition.Id;
                var edgeDefinition = await RegisterEdgeAsync(engine, "knows", nodeDefinition.Id);
                edgeTableId = edgeDefinition.Id;
                sql_storage_engine.Exposures.Catalog.CatalogGraphTableExposure.ReadSupportsOutgoingTraversal(edgeDefinition.Graph!).Should().BeTrue();
                sql_storage_engine.Exposures.Catalog.CatalogGraphTableExposure.ReadSupportsIncomingTraversal(edgeDefinition.Graph!).Should().BeTrue();
                sql_storage_engine.Exposures.Catalog.CatalogGraphTableExposure.ReadTraversalOrder(edgeDefinition.Graph!).Should().Be(GraphTraversalOrder.EdgePhysicalIdentity);
                sql_storage_engine.Exposures.Catalog.CatalogGraphTableExposure.ReadDuplicateSemantics(edgeDefinition.Graph!).Should().Be(GraphDuplicateSemantics.PreserveParallelEdgesAndDeduplicateSelfLoopForBoth);
                sql_storage_engine.Exposures.Catalog.CatalogGraphTableExposure.ReadMaximumEdgesPerTraversal(edgeDefinition.Graph!).Should().Be(GraphTraversalOptions.MaximumSupportedEdges);
                var nodes = await engine.OpenGraphNodeTableAsync(nodeDefinition.Id);
                var edges = await engine.OpenGraphEdgeTableAsync(edgeDefinition.Id);
                first = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("first")]));
                second = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("second")]));
                third = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("third")]));
                using (var cancelledInsert = new CancellationTokenSource())
                {
                    await cancelledInsert.CancelAsync();
                    await ((Func<Task>)(async () => await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("cancelled")]), cancelledInsert.Token))).Should().ThrowAsync<OperationCanceledException>();
                }

                var nodeCount = 0;
                await foreach (var _ in nodes.ScanAsync())
                    nodeCount++;
                nodeCount.Should().Be(3, "pre-admission cancellation cannot create a graph identity or row");
                firstEdge = await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=second},RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("one")]));
                secondEdge = await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=third},RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("two")]));
                var self = await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=first},RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("self")]));
                (await CollectAsync(edges.TraverseAsync(first, GraphEdgeDirection.Outgoing))).Select(edge => edge.EdgeId).Should().BeEquivalentTo([firstEdge, secondEdge, self]);
                (await CollectAsync(edges.TraverseAsync(second, GraphEdgeDirection.Incoming))).Select(edge => edge.EdgeId).Should().Equal(firstEdge);
                (await CollectAsync(edges.TraverseAsync(first, GraphEdgeDirection.Both))).Select(edge => edge.EdgeId).Should().OnlyHaveUniqueItems().And.BeEquivalentTo([firstEdge, secondEdge, self]);
                await ((Func<Task>)(async () => await CollectAsync(edges.TraverseAsync(first, GraphEdgeDirection.Outgoing, sql_storage_engine.Exposures.Graph.GraphTraversalOptionsExposure.CreateGraphTraversalOptions(2))))).Should().ThrowAsync<StorageResourceExhaustedException>();
                using var cancelled = new CancellationTokenSource();
                await cancelled.CancelAsync();
                await ((Func<Task>)(async () => await CollectAsync(edges.TraverseAsync(first, GraphEdgeDirection.Outgoing, cancellationToken: cancelled.Token)))).Should().ThrowAsync<OperationCanceledException>();
                (await nodes.UpdateAsync(first, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('x', 5_000)) }]))).Should().BeTrue();
                (await nodes.GetAsync(first))!.NodeId.Should().Be(first, "logical graph identity survives heap relocation");
                (await edges.UpdateAsync(firstEdge, RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("updated") }]))).Should().BeTrue();
                (await edges.ReconnectAsync(secondEdge,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=second,ToNodeId=third})).Should().BeTrue();
                (await CollectAsync(edges.TraverseAsync(first, GraphEdgeDirection.Outgoing))).Select(edge => edge.EdgeId).Should().BeEquivalentTo([firstEdge, self]);
                (await CollectAsync(edges.TraverseAsync(second, GraphEdgeDirection.Outgoing))).Select(edge => edge.EdgeId).Should().Equal(secondEdge);
                await ((Func<Task>)(async () => await nodes.DeleteAsync(first))).Should().ThrowAsync<GraphReferentialIntegrityException>();
                var missing = sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.CreateGraphNodeId(databaseId:engine.ReadDatabaseId(),tableId:nodeDefinition.Id,schemaVersion:nodeDefinition.SchemaVersion,value:Guid.NewGuid());
                await ((Func<Task>)(async () => await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=missing,ToNodeId=second},RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("invalid")])))).Should().ThrowAsync<GraphReferentialIntegrityException>();
                await ((Func<Task>)(async () => await CollectAsync(edges.TraverseAsync(missing, GraphEdgeDirection.Outgoing)))).Should().ThrowAsync<GraphReferentialIntegrityException>();
                (await CollectAsync(edges.TraverseAsync(first, GraphEdgeDirection.Outgoing))).Should().HaveCount(2, "failed endpoint validation has no side effects");
                await ((Func<Task>)(async () => await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.CreateGraphNodeId(databaseId:DatabaseIdExposure.CreateDatabaseId(),tableId:first.TableId,schemaVersion:first.SchemaVersion,value:first.Value),ToNodeId=second},RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("foreign")])))).Should().ThrowAsync<ArgumentException>();
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var nodesAfterReopen = await reopened.OpenGraphNodeTableAsync(nodeTableId);
            var edgesAfterReopen = await reopened.OpenGraphEdgeTableAsync(edgeTableId);
            (await nodesAfterReopen.GetAsync(second))!.NodeId.Should().Be(second);
            ((TextSqlValue)(await edgesAfterReopen.GetAsync(firstEdge))!.Row.Values[0]).Value.Should().Be("updated");
            (await CollectAsync(edgesAfterReopen.TraverseAsync(second,GraphEdgeDirection.Outgoing))).Select(edge => edge.EdgeId).Should().Equal(secondEdge);
            var persistedEdges = await CollectAsync(edgesAfterReopen.TraverseAsync(first,GraphEdgeDirection.Both));
            foreach (var edge in persistedEdges)
                (await edgesAfterReopen.DeleteAsync(edge.EdgeId)).Should().BeTrue();
            (await edgesAfterReopen.DeleteAsync(secondEdge)).Should().BeTrue();
            (await nodesAfterReopen.DeleteAsync(first)).Should().BeTrue();
            (await nodesAfterReopen.GetAsync(first)).Should().BeNull();
        }
        finally
        {
            Delete(path);
        }
    }

    private static async Task<CatalogTable> RegisterNodeAsync(IStorageEngine engine, string name)
    {
        var table = await CreateNodeTableAsync(engine,name);
        var identity = engine.ReadCatalog().GetIndexes(table.Id).Single(index => index.Name == name + "_graph_id");
        return await engine.RegisterGraphNodeTableAsync(table.Id, new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, identity.Id);
    }

    private static async Task<CatalogTable> CreateNodeTableAsync(IStorageEngine engine, string name)
    {
        var table = await engine.CreateCatalogTableAsync(name,[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharMaxSqlType(), true), GraphColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"$node_id",CatalogGeneratedAlwaysKind.GraphIdentity)]);
        await engine.CreateCatalogIndexAsync(name + "_graph_id",table.Id,true,[Indexed(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 })]);
        return table;
    }

    private static async Task<CatalogTable> RegisterEdgeAsync(IStorageEngine engine, string name, TableId nodeTableId)
    {
        var table = await engine.CreateCatalogTableAsync(name,[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharMaxSqlType(), true), GraphColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"$edge_id",CatalogGeneratedAlwaysKind.GraphIdentity), GraphColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },"$from_id",CatalogGeneratedAlwaysKind.GraphFromNode), GraphColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 },"$to_id",CatalogGeneratedAlwaysKind.GraphToNode)]);
        var identity = await engine.CreateCatalogIndexAsync(name + "_graph_id",table.Id,true,[Indexed(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 })]);
        var outgoing = await engine.CreateCatalogIndexAsync(name + "_from",table.Id,false,[Indexed(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 })]);
        var incoming = await engine.CreateCatalogIndexAsync(name + "_to",table.Id,false,[Indexed(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 })]);
        return await engine.RegisterGraphEdgeTableAsync(tableId:table.Id,catalogGraphTable:new sql_storage_engine.Models.Catalog.CatalogGraphTable {Kind=sql_storage_engine.Catalog.GraphTableKind.Edge,IdentityColumnId=new ColumnId {Value=2},IdentityIndexId=identity.Id,FromNodeTableId=nodeTableId,FromNodeColumnId=new ColumnId {Value=3},OutgoingIndexId=outgoing.Id,ToNodeTableId=nodeTableId,ToNodeColumnId=new ColumnId {Value=4},IncomingIndexId=incoming.Id},cancellationToken:default);
    }

    private static CatalogColumn GraphColumn(ColumnId id, string name, CatalogGeneratedAlwaysKind kind) => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(id, name, sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinarySqlType(sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.EncodedLength), false, generatedAlways: kind, isHidden: true);
    private static CatalogIndexedColumn Indexed(ColumnId id) => sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(id,SortDirection.Ascending,NullSortOrder.First);
    private static async Task<IReadOnlyList<StoredGraphEdge>> CollectAsync(IAsyncEnumerable<StoredGraphEdge> source)
    {
        List<StoredGraphEdge> rows = [];
        await foreach (var row in source)
            rows.Add(row);
        return rows;
    }

    private static string TempPath(string prefix) => Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.db");
    private static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        if (File.Exists(path + ".statement-journal"))
            File.Delete(path + ".statement-journal");
    }
}