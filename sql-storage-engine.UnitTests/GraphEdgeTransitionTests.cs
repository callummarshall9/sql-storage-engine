// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Exposures.Identifiers;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.UnitTests;
public sealed class GraphEdgeTransitionTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task CombinedTransitionChecksOnlyFinalRowAndGeneratesOnce(bool scoped)
    {
        var path = TemporaryPath();
        try
        {
            TableId edgesId;
            GraphEdgeId id;
            GraphNodeId second;
            ulong version;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure storage = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var(nodesId, edgeTableId) = await CreateTablesAsync(storage,constrained: true);
                edgesId = edgeTableId;
                var nodes = await storage.OpenGraphNodeTableAsync(nodesId);
                var first = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]));
                second = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]));
                var edges = await storage.OpenGraphEdgeTableAsync(edgesId);
                id = await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=first},Payload(0));
                version = Version((await edges.GetAsync(id))!);
                if (scoped)
                    await storage.ExecuteStatementAsync(async (statement, token) =>
                    {
                        var graph = await statement.OpenGraphEdgeTableAsync(edgesId,token);
                        Assert.That(await graph.UpdateAndReconnectAsync(id,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=second},ChangePayload(1),token), Is.True);
                    });
                else
                    Assert.That(await edges.UpdateAndReconnectAsync(id,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=second},ChangePayload(1)), Is.True);
                var after = (await edges.GetAsync(id))!;
                Assert.That(after.EdgeId, Is.EqualTo(id));
                Assert.That(after.ToNodeId, Is.EqualTo(second));
                Assert.That(Version(after), Is.EqualTo(version + 1));
                Assert.That(((IntegerSqlValue)after.Row.Values[0]).Value, Is.EqualTo(1));
                Assert.That(((IntegerSqlValue)after.Row.Values[2]).Value, Is.EqualTo(10));
                var adjacency = new List<StoredGraphEdge>();
                await foreach (var item in edges.TraverseAsync(second,GraphEdgeDirection.Incoming))
                    adjacency.Add(item);
                Assert.That(adjacency.Select(item => item.EdgeId), Is.EqualTo(new[] { id }));
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var persisted = (await (await reopened.OpenGraphEdgeTableAsync(edgesId)).GetAsync(id))!;
            Assert.That(persisted.ToNodeId, Is.EqualTo(second));
            Assert.That(Version(persisted), Is.EqualTo(version + 1));
        }
        finally
        {
            Delete(path);
        }
    }

    [TestCase("cancel")]
    [TestCase("check")]
    [TestCase("resource")]
    public async Task FailureAfterCombinedMutationRestoresAllIndexesAndAllowsRetry(string failure)
    {
        var path = TemporaryPath();
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure storage = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var(nodesId, edgesId) = await CreateTablesAsync(storage,constrained: true);
            var payloadIndex = await storage.CreateCatalogIndexAsync("payload_index", edgesId, false, [Indexed(1)]);
            var nodes = await storage.OpenGraphNodeTableAsync(nodesId);
            var first = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]));
            var second = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]));
            var edges = await storage.OpenGraphEdgeTableAsync(edgesId);
            var id = await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=first},Payload(0));
            var version = Version((await edges.GetAsync(id))!);
            using var cancellation = new CancellationTokenSource();
            async Task Mutate() => await storage.ExecuteStatementAsync(async (statement, token) =>
            {
                var graph = await statement.OpenGraphEdgeTableAsync(edgesId, token);
                Assert.That(await graph.UpdateAndReconnectAsync(id,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=second},ChangePayload(1),token), Is.True);
                if (failure == "cancel")
                    cancellation.Cancel();
                if (failure == "check")
                    await graph.UpdateAndReconnectAsync(id,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=second,ToNodeId=second},ChangePayload(1),token);
                if (failure == "resource")
                {
                    await graph.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=second},Payload(1),token);
                    await foreach (var item in graph.TraverseAsync(first, GraphEdgeDirection.Outgoing, sql_storage_engine.Exposures.Graph.GraphTraversalOptionsExposure.CreateGraphTraversalOptions(1), token))
                    {
                    }
                }
            }, cancellation.Token);
            if (failure == "cancel")
                Assert.ThrowsAsync<OperationCanceledException>(Mutate);
            if (failure == "check")
                Assert.ThrowsAsync<ArgumentException>(Mutate);
            if (failure == "resource")
                Assert.ThrowsAsync<StorageResourceExhaustedException>(Mutate);
            edges = await storage.OpenGraphEdgeTableAsync(edgesId);
            var restored = (await edges.GetAsync(id))!;
            Assert.That(restored.FromNodeId, Is.EqualTo(first));
            Assert.That(restored.ToNodeId, Is.EqualTo(first));
            Assert.That(Version(restored), Is.EqualTo(version));
            var index = await storage.OpenIndexAsync(payloadIndex.Id);
            Assert.That(await index.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(0)]), Has.Count.EqualTo(1));
            Assert.That(await index.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]), Is.Empty);
            var incoming = new List<StoredGraphEdge>();
            await foreach (var item in edges.TraverseAsync(second, GraphEdgeDirection.Incoming))
                incoming.Add(item);
            Assert.That(incoming, Is.Empty);
            await storage.ExecuteStatementAsync(async (statement, token) =>
            {
                var graph = await statement.OpenGraphEdgeTableAsync(edgesId, token);
                Assert.That(await graph.UpdateAndReconnectAsync(id,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=second},ChangePayload(1),token), Is.True);
            });
            Assert.That(Version((await edges.GetAsync(id))!), Is.EqualTo(version + 1));
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task InvalidEndpointsGeneratedAssignmentsMissingRowsAndExpiredScopeAreGuarded()
    {
        var path = TemporaryPath();
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure storage = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var(nodesId, edgesId) = await CreateTablesAsync(storage,constrained: true);
            var nodes = await storage.OpenGraphNodeTableAsync(nodesId);
            var first = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]));
            var edges = await storage.OpenGraphEdgeTableAsync(edgesId);
            var id = await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=first},Payload(0));
            var version = Version((await edges.GetAsync(id))!);
            var missing = sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.CreateGraphNodeId(databaseId:first.DatabaseId,tableId:first.TableId,schemaVersion:first.SchemaVersion,value:Guid.NewGuid());
            Assert.ThrowsAsync<GraphReferentialIntegrityException>(async () => await edges.UpdateAndReconnectAsync(id,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=missing},ChangePayload(1)));
            Assert.ThrowsAsync<ArgumentException>(async () => await edges.UpdateAndReconnectAsync(id,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.CreateGraphNodeId(databaseId:DatabaseIdExposure.CreateDatabaseId(),tableId:first.TableId,schemaVersion:first.SchemaVersion,value:first.Value),ToNodeId=first},ChangePayload(0)));
            foreach (var ordinal in new[]
            {
                1,
                2,
                3,
                4,
                5
            }

            )
                Assert.ThrowsAsync<ArgumentException>(async () => await edges.UpdateAndReconnectAsync(id,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=first},RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = ordinal, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue() }])));
            var missingEdge = sql_storage_engine.Exposures.Graph.GraphEdgeIdExposure.CreateGraphEdgeId(databaseId:id.DatabaseId,tableId:id.TableId,schemaVersion:id.SchemaVersion,value:Guid.NewGuid());
            Assert.That(await edges.UpdateAndReconnectAsync(missingEdge,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=first},ChangePayload(0)), Is.False);
            Assert.That(Version((await edges.GetAsync(id))!), Is.EqualTo(version));
            IStorageGraphEdgeTable scoped = null!;
            await storage.ExecuteStatementAsync(async (statement, token) => scoped = await statement.OpenGraphEdgeTableAsync(edgesId, token));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await scoped.UpdateAndReconnectAsync(id,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=first},ChangePayload(0)));
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task ActiveJournalRecoveryRestoresCombinedTransition()
    {
        var path = TemporaryPath();
        try
        {
            TableId edgesId;
            GraphEdgeId id;
            GraphNodeId first;
            ulong version;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure storage = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var(nodesId, edgeTableId) = await CreateTablesAsync(storage,constrained: true);
                edgesId = edgeTableId;
                var nodes = await storage.OpenGraphNodeTableAsync(nodesId);
                first = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]));
                var second = await nodes.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]));
                var edges = await storage.OpenGraphEdgeTableAsync(edgesId);
                id = await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=first},Payload(0));
                version = Version((await edges.GetAsync(id))!);
                await using var journal = await sql_storage_engine.Exposures.Transactions.StatementJournalExposure.CreateStatementJournalExposureAsync(path, CancellationToken.None);
                Assert.That(await edges.UpdateAndReconnectAsync(id,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=second},ChangePayload(1)), Is.True);
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var restored = (await (await reopened.OpenGraphEdgeTableAsync(edgesId)).GetAsync(id))!;
            Assert.That(restored.ToNodeId, Is.EqualTo(first));
            Assert.That(Version(restored), Is.EqualTo(version));
            Assert.That(((IntegerSqlValue)restored.Row.Values[2]).Value, Is.EqualTo(0));
        }
        finally
        {
            Delete(path);
        }
    }

    private static async Task<(TableId Nodes, TableId Edges)> CreateTablesAsync(IStorageEngine storage, bool constrained)
    {
        var node = await storage.CreateCatalogTableAsync("transition_nodes",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), GraphColumn(2,"node_id",CatalogGeneratedAlwaysKind.GraphIdentity)]);
        var nodeIdentity = await storage.CreateCatalogIndexAsync("node_identity",node.Id,true,[Indexed(2)]);
        node = await storage.RegisterGraphNodeTableAsync(node.Id,new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },nodeIdentity.Id);
        var edge = await storage.CreateCatalogTableAsync("transition_edges",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "rv", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeRowVersion(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 }, "computed_payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false, computedExpression: "[payload] * 10", isComputedPersisted: true), GraphColumn(4,"edge_id",CatalogGeneratedAlwaysKind.GraphIdentity), GraphColumn(5,"from_id",CatalogGeneratedAlwaysKind.GraphFromNode), GraphColumn(6,"to_id",CatalogGeneratedAlwaysKind.GraphToNode)],constrained ? [sql_storage_engine.Exposures.Catalog.CatalogCheckConstraintExposure.CreateCatalogCheckConstraint("complete_transition","(payload = 0 AND from_id = to_id) OR (payload = 1 AND from_id <> to_id)")] : []);
        var identity = await storage.CreateCatalogIndexAsync("edge_identity",edge.Id,true,[Indexed(4)]);
        var outgoing = await storage.CreateCatalogIndexAsync("outgoing",edge.Id,false,[Indexed(5)]);
        var incoming = await storage.CreateCatalogIndexAsync("incoming",edge.Id,false,[Indexed(6)]);
        edge = await storage.RegisterGraphEdgeTableAsync(tableId:edge.Id,catalogGraphTable:new sql_storage_engine.Models.Catalog.CatalogGraphTable {Kind=sql_storage_engine.Catalog.GraphTableKind.Edge,IdentityColumnId=new ColumnId {Value=4},IdentityIndexId=identity.Id,FromNodeTableId=node.Id,FromNodeColumnId=new ColumnId {Value=5},OutgoingIndexId=outgoing.Id,ToNodeTableId=node.Id,ToNodeColumnId=new ColumnId {Value=6},IncomingIndexId=incoming.Id},cancellationToken:default);
        return (node.Id, edge.Id);
    }

    private static CatalogColumn GraphColumn(ulong id, string name, CatalogGeneratedAlwaysKind kind) => sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = id }, name, sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateBinarySqlType(sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.EncodedLength), false, generatedAlways: kind, isHidden: true);
    private static CatalogIndexedColumn Indexed(ulong id) => sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = id },SortDirection.Ascending,NullSortOrder.First);
    private static Row Payload(long value) => RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(value), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadDefaultSqlValue()]);
    private static RowUpdate ChangePayload(long value) => RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(value) }]);
    private static ulong Version(StoredGraphEdge edge) => BinaryPrimitives.ReadUInt64BigEndian(sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadBinarySqlValue(binarySqlValue: ((BinarySqlValue)edge.Row.Values[1])).Span);
    private static string TemporaryPath() => Path.Combine(Path.GetTempPath(), $"graph-transition-{Guid.NewGuid():N}.db");
    private static void Delete(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        if (File.Exists(path + ".statement-undo"))
            File.Delete(path + ".statement-undo");
    }
}