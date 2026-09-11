using System.Buffers.Binary;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

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
            await using (var storage = await StorageEngine.CreateAsync(path))
            {
                var (nodesId, edgeTableId) = await CreateTablesAsync(storage, constrained: true);
                edgesId = edgeTableId;
                var nodes = await storage.OpenGraphNodeTableAsync(nodesId);
                var first = await nodes.InsertAsync(new Row([SqlValue.Integer(1)]));
                second = await nodes.InsertAsync(new Row([SqlValue.Integer(2)]));
                var edges = await storage.OpenGraphEdgeTableAsync(edgesId);
                id = await edges.InsertAsync(first, first, Payload(0));
                version = Version((await edges.GetAsync(id))!);
                if (scoped)
                    await storage.ExecuteStatementAsync(async (statement, token) =>
                    {
                        var graph = await statement.OpenGraphEdgeTableAsync(edgesId, token);
                        Assert.That(await graph.UpdateAndReconnectAsync(id, first, second, ChangePayload(1), token), Is.True);
                    });
                else
                    Assert.That(await edges.UpdateAndReconnectAsync(id, first, second, ChangePayload(1)), Is.True);
                var after = (await edges.GetAsync(id))!;
                Assert.That(after.EdgeId, Is.EqualTo(id));
                Assert.That(after.ToNodeId, Is.EqualTo(second));
                Assert.That(Version(after), Is.EqualTo(version + 1));
                Assert.That(((IntegerSqlValue)after.Row.Values[0]).Value, Is.EqualTo(1));
                Assert.That(((IntegerSqlValue)after.Row.Values[2]).Value, Is.EqualTo(10));
                var adjacency = new List<StoredGraphEdge>();
                await foreach (var item in edges.TraverseAsync(second, GraphEdgeDirection.Incoming)) adjacency.Add(item);
                Assert.That(adjacency.Select(item => item.EdgeId), Is.EqualTo(new[] { id }));
            }
            await using var reopened = await StorageEngine.OpenAsync(path);
            var persisted = (await (await reopened.OpenGraphEdgeTableAsync(edgesId)).GetAsync(id))!;
            Assert.That(persisted.ToNodeId, Is.EqualTo(second));
            Assert.That(Version(persisted), Is.EqualTo(version + 1));
        }
        finally { Delete(path); }
    }

    [TestCase("cancel")]
    [TestCase("check")]
    [TestCase("resource")]
    public async Task FailureAfterCombinedMutationRestoresAllIndexesAndAllowsRetry(string failure)
    {
        var path = TemporaryPath();
        try
        {
            await using var storage = await StorageEngine.CreateAsync(path);
            var (nodesId, edgesId) = await CreateTablesAsync(storage, constrained: true);
            var payloadIndex = await storage.CreateIndexAsync("payload_index", edgesId, false, [Indexed(1)]);
            var nodes = await storage.OpenGraphNodeTableAsync(nodesId);
            var first = await nodes.InsertAsync(new Row([SqlValue.Integer(1)]));
            var second = await nodes.InsertAsync(new Row([SqlValue.Integer(2)]));
            var edges = await storage.OpenGraphEdgeTableAsync(edgesId);
            var id = await edges.InsertAsync(first, first, Payload(0));
            var version = Version((await edges.GetAsync(id))!);
            using var cancellation = new CancellationTokenSource();
            async Task Mutate() => await storage.ExecuteStatementAsync(async (statement, token) =>
            {
                var graph = await statement.OpenGraphEdgeTableAsync(edgesId, token);
                Assert.That(await graph.UpdateAndReconnectAsync(id, first, second, ChangePayload(1), token), Is.True);
                if (failure == "cancel") cancellation.Cancel();
                if (failure == "check") await graph.UpdateAndReconnectAsync(id, second, second, ChangePayload(1), token);
                if (failure == "resource")
                {
                    await graph.InsertAsync(first, second, Payload(1), token);
                    await foreach (var item in graph.TraverseAsync(first, GraphEdgeDirection.Outgoing,
                        new GraphTraversalOptions(1), token)) { }
                }
            }, cancellation.Token);
            if (failure == "cancel") Assert.ThrowsAsync<OperationCanceledException>(Mutate);
            if (failure == "check") Assert.ThrowsAsync<ArgumentException>(Mutate);
            if (failure == "resource") Assert.ThrowsAsync<StorageResourceExhaustedException>(Mutate);
            edges = await storage.OpenGraphEdgeTableAsync(edgesId);
            var restored = (await edges.GetAsync(id))!;
            Assert.That(restored.FromNodeId, Is.EqualTo(first));
            Assert.That(restored.ToNodeId, Is.EqualTo(first));
            Assert.That(Version(restored), Is.EqualTo(version));
            var index = await storage.OpenIndexAsync(payloadIndex.Id);
            Assert.That(await index.FindAsync([SqlValue.Integer(0)]), Has.Count.EqualTo(1));
            Assert.That(await index.FindAsync([SqlValue.Integer(1)]), Is.Empty);
            var incoming = new List<StoredGraphEdge>();
            await foreach (var item in edges.TraverseAsync(second, GraphEdgeDirection.Incoming)) incoming.Add(item);
            Assert.That(incoming, Is.Empty);
            await storage.ExecuteStatementAsync(async (statement, token) =>
            {
                var graph = await statement.OpenGraphEdgeTableAsync(edgesId, token);
                Assert.That(await graph.UpdateAndReconnectAsync(id, first, second, ChangePayload(1), token), Is.True);
            });
            Assert.That(Version((await edges.GetAsync(id))!), Is.EqualTo(version + 1));
        }
        finally { Delete(path); }
    }

    [Test]
    public async Task InvalidEndpointsGeneratedAssignmentsMissingRowsAndExpiredScopeAreGuarded()
    {
        var path = TemporaryPath();
        try
        {
            await using var storage = await StorageEngine.CreateAsync(path);
            var (nodesId, edgesId) = await CreateTablesAsync(storage, constrained: true);
            var nodes = await storage.OpenGraphNodeTableAsync(nodesId);
            var first = await nodes.InsertAsync(new Row([SqlValue.Integer(1)]));
            var edges = await storage.OpenGraphEdgeTableAsync(edgesId);
            var id = await edges.InsertAsync(first, first, Payload(0));
            var version = Version((await edges.GetAsync(id))!);
            var missing = new GraphNodeId(first.DatabaseId, first.TableId, first.SchemaVersion, Guid.NewGuid());
            Assert.ThrowsAsync<GraphReferentialIntegrityException>(async () =>
                await edges.UpdateAndReconnectAsync(id, first, missing, ChangePayload(1)));
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await edges.UpdateAndReconnectAsync(id,
                    new GraphNodeId(DatabaseId.New(), first.TableId, first.SchemaVersion, first.Value),
                    first, ChangePayload(0)));
            foreach (var ordinal in new[] { 1, 2, 3, 4, 5 })
                Assert.ThrowsAsync<ArgumentException>(async () =>
                    await edges.UpdateAndReconnectAsync(id, first, first,
                        new RowUpdate([new ColumnUpdate(ordinal, SqlValue.Default)])));
            var missingEdge = new GraphEdgeId(id.DatabaseId, id.TableId, id.SchemaVersion, Guid.NewGuid());
            Assert.That(await edges.UpdateAndReconnectAsync(missingEdge, first, first, ChangePayload(0)), Is.False);
            Assert.That(Version((await edges.GetAsync(id))!), Is.EqualTo(version));
            IStorageGraphEdgeTable scoped = null!;
            await storage.ExecuteStatementAsync(async (statement, token) =>
                scoped = await statement.OpenGraphEdgeTableAsync(edgesId, token));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await scoped.UpdateAndReconnectAsync(id, first, first, ChangePayload(0)));
        }
        finally { Delete(path); }
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
            await using (var storage = await StorageEngine.CreateAsync(path))
            {
                var (nodesId, edgeTableId) = await CreateTablesAsync(storage, constrained: true);
                edgesId = edgeTableId;
                var nodes = await storage.OpenGraphNodeTableAsync(nodesId);
                first = await nodes.InsertAsync(new Row([SqlValue.Integer(1)]));
                var second = await nodes.InsertAsync(new Row([SqlValue.Integer(2)]));
                var edges = await storage.OpenGraphEdgeTableAsync(edgesId);
                id = await edges.InsertAsync(first, first, Payload(0));
                version = Version((await edges.GetAsync(id))!);
                await using var journal = await Transactions.StatementJournal.CreateAsync(path, CancellationToken.None);
                Assert.That(await edges.UpdateAndReconnectAsync(id, first, second, ChangePayload(1)), Is.True);
            }
            await using var reopened = await StorageEngine.OpenAsync(path);
            var restored = (await (await reopened.OpenGraphEdgeTableAsync(edgesId)).GetAsync(id))!;
            Assert.That(restored.ToNodeId, Is.EqualTo(first));
            Assert.That(Version(restored), Is.EqualTo(version));
            Assert.That(((IntegerSqlValue)restored.Row.Values[2]).Value, Is.EqualTo(0));
        }
        finally { Delete(path); }
    }

    private static async Task<(TableId Nodes, TableId Edges)> CreateTablesAsync(
        IStorageEngine storage, bool constrained)
    {
        var node = await storage.CreateTableAsync("transition_nodes",
        [
            new CatalogColumn(new ColumnId(1), "payload", SqlType.Int, false),
            GraphColumn(2, "node_id", CatalogGeneratedAlwaysKind.GraphIdentity)
        ]);
        var nodeIdentity = await storage.CreateIndexAsync("node_identity", node.Id, true, [Indexed(2)]);
        node = await storage.RegisterGraphNodeTableAsync(node.Id, new ColumnId(2), nodeIdentity.Id);
        var edge = await storage.CreateTableAsync("transition_edges",
        [
            new CatalogColumn(new ColumnId(1), "payload", SqlType.Int, false),
            new CatalogColumn(new ColumnId(2), "rv", SqlType.RowVersion, false),
            new CatalogColumn(new ColumnId(3), "computed_payload", SqlType.Int, false,
                computedExpression: "[payload] * 10", isComputedPersisted: true),
            GraphColumn(4, "edge_id", CatalogGeneratedAlwaysKind.GraphIdentity),
            GraphColumn(5, "from_id", CatalogGeneratedAlwaysKind.GraphFromNode),
            GraphColumn(6, "to_id", CatalogGeneratedAlwaysKind.GraphToNode)
        ], constrained
            ? [new CatalogCheckConstraint("complete_transition",
                "(payload = 0 AND from_id = to_id) OR (payload = 1 AND from_id <> to_id)")]
            : []);
        var identity = await storage.CreateIndexAsync("edge_identity", edge.Id, true, [Indexed(4)]);
        var outgoing = await storage.CreateIndexAsync("outgoing", edge.Id, false, [Indexed(5)]);
        var incoming = await storage.CreateIndexAsync("incoming", edge.Id, false, [Indexed(6)]);
        edge = await storage.RegisterGraphEdgeTableAsync(edge.Id, new ColumnId(4), identity.Id,
            node.Id, new ColumnId(5), outgoing.Id, node.Id, new ColumnId(6), incoming.Id);
        return (node.Id, edge.Id);
    }

    private static CatalogColumn GraphColumn(ulong id, string name, CatalogGeneratedAlwaysKind kind) =>
        new(new ColumnId(id), name, SqlType.Binary(GraphNodeId.EncodedLength), false,
            generatedAlways: kind, isHidden: true);

    private static CatalogIndexedColumn Indexed(ulong id) =>
        new(new ColumnId(id), SortDirection.Ascending, NullSortOrder.First);

    private static Row Payload(long value) => new([SqlValue.Integer(value), SqlValue.Default, SqlValue.Default]);
    private static RowUpdate ChangePayload(long value) => new([new ColumnUpdate(0, SqlValue.Integer(value))]);
    private static ulong Version(StoredGraphEdge edge) =>
        BinaryPrimitives.ReadUInt64BigEndian(((BinarySqlValue)edge.Row.Values[1]).Value.Span);
    private static string TemporaryPath() => Path.Combine(Path.GetTempPath(), $"graph-transition-{Guid.NewGuid():N}.db");
    private static void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".statement-undo")) File.Delete(path + ".statement-undo");
    }
}
