using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;

namespace sql_storage_engine.UnitTests;

public sealed class GraphStorageTests
{
    [Test]
    public void TypedIdentitiesRoundTripAndRejectWrongKindsAndMalformedValues()
    {
        var databaseId = DatabaseId.New();
        var node = new GraphNodeId(databaseId, new TableId(2), 3, Guid.NewGuid());
        var edge = new GraphEdgeId(databaseId, new TableId(4), 5, Guid.NewGuid());

        GraphNodeId.FromSqlValue(node.ToSqlValue()).Should().Be(node);
        GraphEdgeId.FromSqlValue(edge.ToSqlValue()).Should().Be(edge);
        ((Func<GraphNodeId>)(() => GraphNodeId.FromSqlValue(edge.ToSqlValue())))
            .Should().Throw<ArgumentException>();
        ((Func<GraphNodeId>)(() => GraphNodeId.FromSqlValue(SqlValue.Binary([1, 2]))))
            .Should().Throw<ArgumentException>();
        ((Func<GraphTraversalOptions>)(() => new GraphTraversalOptions(0)))
            .Should().Throw<ArgumentOutOfRangeException>();
        ((Func<GraphTraversalOptions>)(() => new GraphTraversalOptions(
                GraphTraversalOptions.MaximumSupportedEdges + 1)))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task GraphRegistrationPersistsAndOrdinaryMutationCannotBypassGeneratedIdentity()
    {
        var path = TempPath("graph-catalog");
        try
        {
            TableId nodeTableId;
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var nodeTable = await CreateNodeTableAsync(engine, "nodes");
                nodeTableId = nodeTable.Id;
                var ordinary = await engine.OpenTableAsync(nodeTable.Id);
                var identity = engine.Catalog.GetIndexes(nodeTable.Id).Single(index => index.Name == "nodes_graph_id");
                var registered = await engine.RegisterGraphNodeTableAsync(nodeTable.Id, new ColumnId(2), identity.Id);

                registered.SchemaVersion.Should().Be(2);
                registered.Graph!.Kind.Should().Be(GraphTableKind.Node);
                await ((Func<Task>)(async () => await ordinary.InsertAsync(new Row([
                        SqlValue.Text("stale"), SqlValue.Binary(new byte[GraphNodeId.EncodedLength])
                    ]))))
                    .Should().ThrowAsync<InvalidOperationException>();
                var currentOrdinary = await engine.OpenTableAsync(nodeTable.Id);
                await ((Func<Task>)(async () => await currentOrdinary.InsertAsync(new Row([
                        SqlValue.Text("forged"), SqlValue.Binary(new byte[GraphNodeId.EncodedLength])
                    ]))))
                    .Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("*graph storage handle*");
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            var persisted = reopened.Catalog.Tables.Single(table => table.Id == nodeTableId);
            persisted.Graph!.Kind.Should().Be(GraphTableKind.Node);
            persisted.Graph.IdentityColumnId.Should().Be(new ColumnId(2));
            (await reopened.OpenGraphNodeTableAsync(nodeTableId)).Definition.SchemaVersion.Should().Be(2);
        }
        finally { Delete(path); }
    }

    [Test]
    public async Task RegistrationRejectsMalformedMetadataAndNonemptyTablesWithoutPublishingGraphScope()
    {
        var path = TempPath("graph-registration-failure");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var wrong = await engine.CreateTableAsync("wrong", [
                new CatalogColumn(new ColumnId(1), "payload", SqlType.Int, false),
                GraphColumn(new ColumnId(2), "$node_id", CatalogGeneratedAlwaysKind.GraphIdentity)
            ]);
            var nonUnique = await engine.CreateIndexAsync("wrong_graph_id", wrong.Id, false,
                [Indexed(new ColumnId(2))]);

            await ((Func<Task>)(async () => await engine.RegisterGraphNodeTableAsync(
                    wrong.Id, new ColumnId(2), nonUnique.Id)))
                .Should().ThrowAsync<ArgumentException>();
            engine.Catalog.Tables.Single(table => table.Id == wrong.Id).Graph.Should().BeNull();

            var populated = await engine.CreateTableAsync("populated", [
                new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false)
            ]);
            var populatedRows = await engine.OpenTableAsync(populated.Id);
            await populatedRows.InsertAsync(new Row([SqlValue.Integer(1)]));
            var populatedIndex = await engine.CreateIndexAsync("populated_id", populated.Id, true,
                [Indexed(new ColumnId(1))]);
            await ((Func<Task>)(async () => await engine.RegisterGraphNodeTableAsync(
                    populated.Id, new ColumnId(1), populatedIndex.Id)))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*must be empty*");
            engine.Catalog.Tables.Single(table => table.Id == populated.Id).Graph.Should().BeNull();
        }
        finally { Delete(path); }
    }

    [Test]
    public async Task GraphInsertPreservesOrdinaryPayloadGeneration()
    {
        var path = TempPath("graph-generated-payload");
        try
        {
            await using var engine = await StorageEngine.CreateAsync(path);
            var table = await engine.CreateTableAsync("generated_nodes", [
                new CatalogColumn(new ColumnId(1), "id", SqlType.Int, false,
                    identity: new CatalogIdentity(10, 2)),
                new CatalogColumn(new ColumnId(2), "quantity", SqlType.Int, false, defaultExpression: "2"),
                new CatalogColumn(new ColumnId(3), "total", SqlType.Int, false,
                    computedExpression: "[quantity] * 3", isComputedPersisted: true),
                new CatalogColumn(new ColumnId(4), "rv", SqlType.RowVersion, false),
                GraphColumn(new ColumnId(5), "$node_id", CatalogGeneratedAlwaysKind.GraphIdentity)
            ]);
            var identity = await engine.CreateIndexAsync("generated_nodes_graph_id", table.Id, true,
                [Indexed(new ColumnId(5))]);
            await engine.CreateIndexAsync("generated_nodes_quantity", table.Id, true,
                [Indexed(new ColumnId(2))], new CatalogBTreeIndexOptions(ignoreDuplicateKey: true));
            await engine.RegisterGraphNodeTableAsync(table.Id, new ColumnId(5), identity.Id);
            var nodes = await engine.OpenGraphNodeTableAsync(table.Id);

            var nodeId = await nodes.InsertAsync(new Row([
                SqlValue.Default, SqlValue.Default, SqlValue.Default, SqlValue.Null
            ]));
            var stored = (await nodes.GetAsync(nodeId))!.Row;

            stored.Values[0].Should().Be(SqlValue.Integer(10));
            stored.Values[1].Should().Be(SqlValue.Integer(2));
            stored.Values[2].Should().Be(SqlValue.Integer(6));
            stored.Values[3].Should().BeOfType<BinarySqlValue>();
            await ((Func<Task>)(async () => await nodes.InsertAsync(new Row([
                    SqlValue.Default, SqlValue.Default, SqlValue.Default, SqlValue.Null
                ]))))
                .Should().ThrowAsync<DuplicateKeyIgnoredException>();
            var count = 0;
            await foreach (var _ in nodes.ScanAsync()) count++;
            count.Should().Be(1);
        }
        finally { Delete(path); }
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
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                var nodeDefinition = await RegisterNodeAsync(engine, "people");
                nodeTableId = nodeDefinition.Id;
                var edgeDefinition = await RegisterEdgeAsync(engine, "knows", nodeDefinition.Id);
                edgeTableId = edgeDefinition.Id;
                edgeDefinition.Graph!.SupportsOutgoingTraversal.Should().BeTrue();
                edgeDefinition.Graph.SupportsIncomingTraversal.Should().BeTrue();
                edgeDefinition.Graph.TraversalOrder.Should().Be(GraphTraversalOrder.EdgePhysicalIdentity);
                edgeDefinition.Graph.DuplicateSemantics.Should().Be(
                    GraphDuplicateSemantics.PreserveParallelEdgesAndDeduplicateSelfLoopForBoth);
                edgeDefinition.Graph.MaximumEdgesPerTraversal.Should().Be(
                    GraphTraversalOptions.MaximumSupportedEdges);
                var nodes = await engine.OpenGraphNodeTableAsync(nodeDefinition.Id);
                var edges = await engine.OpenGraphEdgeTableAsync(edgeDefinition.Id);

                first = await nodes.InsertAsync(new Row([SqlValue.Text("first")]));
                second = await nodes.InsertAsync(new Row([SqlValue.Text("second")]));
                third = await nodes.InsertAsync(new Row([SqlValue.Text("third")]));
                using (var cancelledInsert = new CancellationTokenSource())
                {
                    await cancelledInsert.CancelAsync();
                    await ((Func<Task>)(async () => await nodes.InsertAsync(
                            new Row([SqlValue.Text("cancelled")]), cancelledInsert.Token)))
                        .Should().ThrowAsync<OperationCanceledException>();
                }
                var nodeCount = 0;
                await foreach (var _ in nodes.ScanAsync()) nodeCount++;
                nodeCount.Should().Be(3, "pre-admission cancellation cannot create a graph identity or row");
                firstEdge = await edges.InsertAsync(first, second, new Row([SqlValue.Text("one")]));
                secondEdge = await edges.InsertAsync(first, third, new Row([SqlValue.Text("two")]));
                var self = await edges.InsertAsync(first, first, new Row([SqlValue.Text("self")]));

                (await CollectAsync(edges.TraverseAsync(first, GraphEdgeDirection.Outgoing)))
                    .Select(edge => edge.EdgeId).Should().BeEquivalentTo([firstEdge, secondEdge, self]);
                (await CollectAsync(edges.TraverseAsync(second, GraphEdgeDirection.Incoming)))
                    .Select(edge => edge.EdgeId).Should().Equal(firstEdge);
                (await CollectAsync(edges.TraverseAsync(first, GraphEdgeDirection.Both)))
                    .Select(edge => edge.EdgeId).Should().OnlyHaveUniqueItems()
                    .And.BeEquivalentTo([firstEdge, secondEdge, self]);

                await ((Func<Task>)(async () => await CollectAsync(edges.TraverseAsync(first,
                        GraphEdgeDirection.Outgoing, new GraphTraversalOptions(2)))))
                    .Should().ThrowAsync<StorageResourceExhaustedException>();
                using var cancelled = new CancellationTokenSource();
                await cancelled.CancelAsync();
                await ((Func<Task>)(async () => await CollectAsync(edges.TraverseAsync(first,
                        GraphEdgeDirection.Outgoing, cancellationToken: cancelled.Token))))
                    .Should().ThrowAsync<OperationCanceledException>();

                (await nodes.UpdateAsync(first, new RowUpdate([
                    new ColumnUpdate(0, SqlValue.Text(new string('x', 5_000)))
                ]))).Should().BeTrue();
                (await nodes.GetAsync(first))!.NodeId.Should().Be(first,
                    "logical graph identity survives heap relocation");
                (await edges.UpdateAsync(firstEdge, new RowUpdate([
                    new ColumnUpdate(0, SqlValue.Text("updated"))
                ]))).Should().BeTrue();
                (await edges.ReconnectAsync(secondEdge, second, third)).Should().BeTrue();
                (await CollectAsync(edges.TraverseAsync(first, GraphEdgeDirection.Outgoing)))
                    .Select(edge => edge.EdgeId).Should().BeEquivalentTo([firstEdge, self]);
                (await CollectAsync(edges.TraverseAsync(second, GraphEdgeDirection.Outgoing)))
                    .Select(edge => edge.EdgeId).Should().Equal(secondEdge);

                await ((Func<Task>)(async () => await nodes.DeleteAsync(first)))
                    .Should().ThrowAsync<GraphReferentialIntegrityException>();
                var missing = new GraphNodeId(engine.DatabaseId, nodeDefinition.Id,
                    nodeDefinition.SchemaVersion, Guid.NewGuid());
                await ((Func<Task>)(async () => await edges.InsertAsync(missing, second,
                        new Row([SqlValue.Text("invalid")]))))
                    .Should().ThrowAsync<GraphReferentialIntegrityException>();
                await ((Func<Task>)(async () => await CollectAsync(edges.TraverseAsync(missing,
                        GraphEdgeDirection.Outgoing))))
                    .Should().ThrowAsync<GraphReferentialIntegrityException>();
                (await CollectAsync(edges.TraverseAsync(first, GraphEdgeDirection.Outgoing)))
                    .Should().HaveCount(2, "failed endpoint validation has no side effects");
                await ((Func<Task>)(async () => await edges.InsertAsync(
                        new GraphNodeId(DatabaseId.New(), first.TableId, first.SchemaVersion, first.Value), second,
                        new Row([SqlValue.Text("foreign")]))))
                    .Should().ThrowAsync<ArgumentException>();
            }

            await using var reopened = await StorageEngine.OpenAsync(path);
            var nodesAfterReopen = await reopened.OpenGraphNodeTableAsync(nodeTableId);
            var edgesAfterReopen = await reopened.OpenGraphEdgeTableAsync(edgeTableId);
            (await nodesAfterReopen.GetAsync(second))!.NodeId.Should().Be(second);
            ((TextSqlValue)(await edgesAfterReopen.GetAsync(firstEdge))!.Row.Values[0]).Value.Should().Be("updated");
            (await CollectAsync(edgesAfterReopen.TraverseAsync(second, GraphEdgeDirection.Outgoing)))
                .Select(edge => edge.EdgeId).Should().Equal(secondEdge);
            var persistedEdges = await CollectAsync(edgesAfterReopen.TraverseAsync(first, GraphEdgeDirection.Both));
            foreach (var edge in persistedEdges) (await edgesAfterReopen.DeleteAsync(edge.EdgeId)).Should().BeTrue();
            (await edgesAfterReopen.DeleteAsync(secondEdge)).Should().BeTrue();
            (await nodesAfterReopen.DeleteAsync(first)).Should().BeTrue();
            (await nodesAfterReopen.GetAsync(first)).Should().BeNull();
        }
        finally { Delete(path); }
    }

    private static async Task<CatalogTable> RegisterNodeAsync(IStorageEngine engine, string name)
    {
        var table = await CreateNodeTableAsync(engine, name);
        var identity = engine.Catalog.GetIndexes(table.Id).Single(index => index.Name == name + "_graph_id");
        return await engine.RegisterGraphNodeTableAsync(table.Id, new ColumnId(2), identity.Id);
    }

    private static async Task<CatalogTable> CreateNodeTableAsync(IStorageEngine engine, string name)
    {
        var table = await engine.CreateTableAsync(name, [
            new CatalogColumn(new ColumnId(1), "payload", SqlType.NVarCharMax(), true),
            GraphColumn(new ColumnId(2), "$node_id", CatalogGeneratedAlwaysKind.GraphIdentity)
        ]);
        await engine.CreateIndexAsync(name + "_graph_id", table.Id, true,
            [Indexed(new ColumnId(2))]);
        return table;
    }

    private static async Task<CatalogTable> RegisterEdgeAsync(IStorageEngine engine, string name,
        TableId nodeTableId)
    {
        var table = await engine.CreateTableAsync(name, [
            new CatalogColumn(new ColumnId(1), "payload", SqlType.NVarCharMax(), true),
            GraphColumn(new ColumnId(2), "$edge_id", CatalogGeneratedAlwaysKind.GraphIdentity),
            GraphColumn(new ColumnId(3), "$from_id", CatalogGeneratedAlwaysKind.GraphFromNode),
            GraphColumn(new ColumnId(4), "$to_id", CatalogGeneratedAlwaysKind.GraphToNode)
        ]);
        var identity = await engine.CreateIndexAsync(name + "_graph_id", table.Id, true,
            [Indexed(new ColumnId(2))]);
        var outgoing = await engine.CreateIndexAsync(name + "_from", table.Id, false,
            [Indexed(new ColumnId(3))]);
        var incoming = await engine.CreateIndexAsync(name + "_to", table.Id, false,
            [Indexed(new ColumnId(4))]);
        return await engine.RegisterGraphEdgeTableAsync(table.Id, new ColumnId(2), identity.Id,
            nodeTableId, new ColumnId(3), outgoing.Id, nodeTableId, new ColumnId(4), incoming.Id);
    }

    private static CatalogColumn GraphColumn(ColumnId id, string name, CatalogGeneratedAlwaysKind kind) =>
        new(id, name, SqlType.Binary(GraphNodeId.EncodedLength), false, generatedAlways: kind, isHidden: true);

    private static CatalogIndexedColumn Indexed(ColumnId id) =>
        new(id, SortDirection.Ascending, NullSortOrder.First);

    private static async Task<IReadOnlyList<StoredGraphEdge>> CollectAsync(
        IAsyncEnumerable<StoredGraphEdge> source)
    {
        List<StoredGraphEdge> rows = [];
        await foreach (var row in source) rows.Add(row);
        return rows;
    }

    private static string TempPath(string prefix) =>
        Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.db");

    private static void Delete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".statement-journal")) File.Delete(path + ".statement-journal");
    }
}
