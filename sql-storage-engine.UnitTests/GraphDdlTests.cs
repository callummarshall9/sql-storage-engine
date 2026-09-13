using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class GraphDdlTests
{
    [Test]
    public async Task CreationPublishesGeneratedColumnsIndexesAndUsableEndpointContractAfterReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"graph-ddl-{Guid.NewGuid():N}.db");
        try
        {
            TableId nodesId = default, edgesId = default;
            GraphNodeId nodeId = default;
            GraphEdgeId edgeId = default;
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                await engine.ExecuteStatementAsync(async (statement, token) =>
                {
                    var nodes = await statement.CreateGraphNodeTableAsync(Name("nodes"), Payload, token);
                    var edges = await statement.CreateGraphEdgeTableAsync(Name("edges"), Payload, nodes.Id, nodes.Id, token);
                    nodesId = nodes.Id; edgesId = edges.Id;
                    Assert.That(nodes.Columns[^1].IsHidden, Is.True);
                    Assert.That(edges.Columns.TakeLast(3).All(column => column.IsHidden), Is.True);
                    Assert.That(engine.Catalog.GetIndexes(nodes.Id), Has.Count.EqualTo(1));
                    Assert.That(engine.Catalog.GetIndexes(edges.Id), Has.Count.EqualTo(3));
                    var nodeTable = await statement.OpenGraphNodeTableAsync(nodes.Id, token);
                    nodeId = await nodeTable.InsertAsync(new Row([SqlValue.Integer(1)]), token);
                    edgeId = await (await statement.OpenGraphEdgeTableAsync(edges.Id, token)).InsertAsync(nodeId, nodeId,
                        new Row([SqlValue.Integer(2)]), token);
                });
            }
            await using var reopened = await StorageEngine.OpenAsync(path);
            var nodeHandle = await reopened.OpenGraphNodeTableAsync(nodesId);
            Assert.That(await nodeHandle.GetAsync(nodeId), Is.Not.Null);
            Assert.That(await (await reopened.OpenGraphEdgeTableAsync(edgesId)).GetAsync(edgeId), Is.Not.Null);
            Assert.ThrowsAsync<GraphReferentialIntegrityException>(async () => await nodeHandle.DeleteAsync(nodeId));
        }
        finally { Delete(path); }
    }

    [TestCase("unknown")]
    [TestCase("ordinary")]
    [TestCase("reserved")]
    [TestCase("populated")]
    [TestCase("duplicate")]
    public async Task InvalidRequestsPreserveEarlierCatalogAndCanRetry(string failure)
    {
        var path = Path.Combine(Path.GetTempPath(), $"graph-ddl-{Guid.NewGuid():N}.db");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var ordinary = await engine.CreateTableAsync("existing", Payload);
            await (await engine.OpenTableAsync(ordinary.Id)).InsertAsync(new Row([SqlValue.Integer(9)]));
            Assert.CatchAsync(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var node = await statement.CreateGraphNodeTableAsync(Name("temporary"), Payload, token);
                if (failure == "unknown" || failure == "ordinary")
                    await statement.CreateGraphEdgeTableAsync(Name("bad"), Payload,
                        failure == "unknown" ? new TableId(999) : ordinary.Id, node.Id, token);
                else if (failure == "reserved") await statement.CreateGraphNodeTableAsync(Name("bad"),
                    [new CatalogColumn(new ColumnId(1), "$node_id", SqlType.Int, false)], token);
                else await statement.CreateGraphNodeTableAsync(Name(failure == "populated" ? "existing" : "temporary"), Payload, token);
            }));
            Assert.That(engine.Catalog.TryGetTable("temporary", out _), Is.False);
            Assert.That(engine.Catalog.TryGetTable("bad", out _), Is.False);
            Assert.That(engine.Catalog.TryGetTable(ordinary.Id, out var restored), Is.True);
            Assert.That(restored!.Graph, Is.Null);
            await engine.ExecuteStatementAsync(async (statement, token) =>
                await statement.CreateGraphNodeTableAsync(Name("retry"), Payload, token));
        }
        finally { Delete(path); }
    }

    [Test]
    public async Task CompletedAndFailedScopesCannotCreateTables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"graph-ddl-{Guid.NewGuid():N}.db");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            IStorageStatement? escaped = null;
            await engine.ExecuteStatementAsync((statement, _) => { escaped = statement; return ValueTask.CompletedTask; });
            Assert.ThrowsAsync<InvalidOperationException>(async () => await escaped!.CreateGraphNodeTableAsync(Name("late"), Payload));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                escaped = statement;
                try { await statement.CreateGraphEdgeTableAsync(Name("invalid"), Payload, new TableId(999), new TableId(999), token); }
                catch (ArgumentException) { }
            }));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await escaped!.CreateGraphNodeTableAsync(Name("late"), Payload));
            Assert.That(engine.Catalog.TryGetTable("late", out _), Is.False);
        }
        finally { Delete(path); }
    }
    [Test]
    public async Task DistinctEndpointRolesRejectReversedPairAndOrdinaryMutationOfGeneratedColumns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"graph-ddl-pair-{Guid.NewGuid():N}.db");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var from = await statement.CreateGraphNodeTableAsync(Name("from_nodes"), Payload, token);
                var to = await statement.CreateGraphNodeTableAsync(Name("to_nodes"), Payload, token);
                var edges = await statement.CreateGraphEdgeTableAsync(Name("edges"), Payload, from.Id, to.Id, token);
                var first = await (await statement.OpenGraphNodeTableAsync(from.Id, token)).InsertAsync(new Row([SqlValue.Integer(1)]), token);
                var second = await (await statement.OpenGraphNodeTableAsync(to.Id, token)).InsertAsync(new Row([SqlValue.Integer(2)]), token);
                var handle = await statement.OpenGraphEdgeTableAsync(edges.Id, token);
                await handle.InsertAsync(first, second, new Row([SqlValue.Integer(3)]), token);
                Assert.ThrowsAsync<ArgumentException>(async () => await handle.InsertAsync(second, first, new Row([SqlValue.Integer(4)]), token));
                var ordinary = await statement.OpenTableAsync(edges.Id, token);
                Assert.ThrowsAsync<InvalidOperationException>(async () => await ordinary.InsertAsync(new Row([SqlValue.Integer(5)]), token));
            });
        }
        finally { Delete(path); }
    }
    internal static CatalogTableName Name(string name) => new("default", "dbo", name);
    internal static CatalogColumn[] Payload => [new(new ColumnId(1), "value", SqlType.Int, false)];
    internal static void Delete(string path)
    {
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*")) File.Delete(file);
    }
}
