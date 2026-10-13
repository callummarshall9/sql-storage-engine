// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

using sql_storage_engine.Models.Graph;

namespace sql_storage_engine.UnitTests;
public sealed class GraphStatementTests
{
    [TestCase("commit")]
    [TestCase("validation")]
    [TestCase("cancellation")]
    [TestCase("resource")]
    public async Task MixedMutationsCommitOrRestoreEveryGraphIndexAndCanRetry(string outcome)
    {
        var path = TempPath("graph-statement");
        try
        {
            TableId nodesId;
            TableId edgesId;
            GraphNodeId first;
            GraphNodeId second;
            GraphEdgeId original;
            GraphNodeId added = default;
            GraphEdgeId inserted = default;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                var nodeDefinition = await RegisterNodeAsync(engine,"nodes");
                var edgeDefinition = await RegisterEdgeAsync(engine, "edges", nodeDefinition.Id);
                var ordinaryDefinition = await engine.CreateCatalogTableAsync("ordinary",[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)]);
                nodesId = nodeDefinition.Id;
                edgesId = edgeDefinition.Id;
                var payloadIndex = await engine.CreateCatalogIndexAsync("payload",edgesId,false,[Indexed(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 })]);
                var rootNodes = await engine.OpenGraphNodeTableAsync(nodesId);
                var rootEdges = await engine.OpenGraphEdgeTableAsync(edgesId);
                first = await rootNodes.InsertAsync(Payload("first"));
                second = await rootNodes.InsertAsync(Payload("second"));
                original = await rootEdges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=second},Payload("original"));
                using var cancellation = new CancellationTokenSource();
                async ValueTask Mutate(IStorageStatement statement, CancellationToken token)
                {
                    var ordinary = await statement.OpenTableAsync(ordinaryDefinition.Id, token);
                    await ordinary.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(42)]), token);
                    var nodes = await statement.OpenGraphNodeTableAsync(nodesId, token);
                    var edges = await statement.OpenGraphEdgeTableAsync(edgesId, token);
                    added = await nodes.InsertAsync(Payload("added"), token);
                    await nodes.UpdateAsync(second, Update("changed"), token);
                    await edges.UpdateAsync(original, Update("updated"), token);
                    await edges.ReconnectAsync(original,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=second,ToNodeId=added},token);
                    inserted = await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=added,ToNodeId=second},Payload("inserted"),token);
                    var disposable = await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=added,ToNodeId=added},Payload("temporary"),token);
                    (await edges.DeleteAsync(disposable, token)).Should().BeTrue();
                    (await nodes.DeleteAsync(first, token)).Should().BeTrue();
                    (await CollectAsync(edges.TraverseAsync(added, GraphEdgeDirection.Both, cancellationToken: token))).Should().HaveCount(2);
                    if (outcome == "validation")
                        await nodes.DeleteAsync(added, token);
                    if (outcome == "resource")
                        await CollectAsync(edges.TraverseAsync(added, GraphEdgeDirection.Both, sql_storage_engine.Exposures.Graph.GraphTraversalOptionsExposure.CreateGraphTraversalOptions(1), token));
                    if (outcome == "cancellation")
                        cancellation.Cancel();
                }

                if (outcome == "commit")
                    await engine.ExecuteStatementAsync(Mutate, cancellation.Token);
                else
                {
                    Func<Task> act = async () => await engine.ExecuteStatementAsync(Mutate, cancellation.Token);
                    if (outcome == "validation")
                        await act.Should().ThrowAsync<GraphReferentialIntegrityException>();
                    else if (outcome == "resource")
                        await act.Should().ThrowAsync<StorageResourceExhaustedException>();
                    else
                        await act.Should().ThrowAsync<OperationCanceledException>();
                }

                var nodesAfter = await engine.OpenGraphNodeTableAsync(nodesId);
                var edgesAfter = await engine.OpenGraphEdgeTableAsync(edgesId);
                var indexAfter = await engine.OpenIndexAsync(payloadIndex.Id);
                var ordinaryCount = 0;
                await foreach (var row in (await engine.OpenTableAsync(ordinaryDefinition.Id)).ScanAsync())
                    ordinaryCount++;
                ordinaryCount.Should().Be(outcome == "commit" ? 1 : 0);
                if (outcome != "commit")
                {
                    (await nodesAfter.GetAsync(added)).Should().BeNull();
                    (await edgesAfter.GetAsync(inserted)).Should().BeNull();
                    (await nodesAfter.GetAsync(first)).Should().NotBeNull();
                    ((TextSqlValue)(await nodesAfter.GetAsync(second))!.Row.Values[0]).Value.Should().Be("second");
                    (await edgesAfter.GetAsync(original))!.FromNodeId.Should().Be(first);
                    (await CollectAsync(edgesAfter.TraverseAsync(first,GraphEdgeDirection.Outgoing))).Should().HaveCount(1);
                    (await CollectAsync(edgesAfter.TraverseAsync(second,GraphEdgeDirection.Incoming))).Should().HaveCount(1);
                    (await indexAfter.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("original")])).Should().HaveCount(1);
                    (await indexAfter.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("updated")])).Should().BeEmpty();
                    await ((Func<Task>)(async () => await rootNodes.GetAsync(first))).Should().ThrowAsync<InvalidOperationException>();
                    await engine.ExecuteStatementAsync(async (statement, token) =>
                    {
                        var edges = await statement.OpenGraphEdgeTableAsync(edgesId,token);
                        await edges.UpdateAsync(original,Update("retry"),token);
                    });
                }
                else
                {
                    (await nodesAfter.GetAsync(first)).Should().BeNull();
                    (await edgesAfter.GetAsync(original))!.ToNodeId.Should().Be(added);
                    (await indexAfter.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("updated")])).Should().HaveCount(1);
                    (await indexAfter.FindAsync([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("original")])).Should().BeEmpty();
                }
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var persisted = await (await reopened.OpenGraphEdgeTableAsync(edgesId)).GetAsync(original);
            ((TextSqlValue)persisted!.Row.Values[0]).Value.Should().Be(outcome == "commit" ? "updated" : "retry");
            File.Exists(path + ".statement-undo").Should().BeFalse();
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task CompletedHandlesEnumeratorsAndWrongKindsFailWithoutBypassingInvariants()
    {
        var path = TempPath("graph-scope");
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var node = await RegisterNodeAsync(engine,"nodes");
            var edge = await RegisterEdgeAsync(engine, "edges", node.Id);
            IStorageStatement scope = null!;
            IStorageGraphNodeTable nodes = null!;
            IStorageGraphEdgeTable edges = null!;
            IAsyncEnumerator<StoredGraphNode> scan = null!;
            GraphNodeId id = default;
            await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                scope = statement;
                nodes = await statement.OpenGraphNodeTableAsync(node.Id, token);
                edges = await statement.OpenGraphEdgeTableAsync(edge.Id, token);
                id = await nodes.InsertAsync(Payload("one"),token);
                await nodes.InsertAsync(Payload("two"),token);
                scan = nodes.ScanAsync(token).GetAsyncEnumerator(token);
                (await scan.MoveNextAsync()).Should().BeTrue();
                await ((Func<Task>)(async () => await statement.OpenGraphNodeTableAsync(edge.Id, token))).Should().ThrowAsync<InvalidOperationException>();
                await ((Func<Task>)(async () => await statement.OpenGraphEdgeTableAsync(node.Id, token))).Should().ThrowAsync<InvalidOperationException>();
                await ((Func<Task>)(async () => await nodes.UpdateAsync(id,RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 1, Value = sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.ToSqlValue(graphNodeId:id) }]),token))).Should().ThrowAsync<ArgumentException>();
                var ordinary = await statement.OpenTableAsync(node.Id, token);
                await ((Func<Task>)(async () => await ordinary.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("forged"), sql_storage_engine.Exposures.Graph.GraphNodeIdExposure.ToSqlValue(graphNodeId:id)]), token))).Should().ThrowAsync<InvalidOperationException>();
            });
            await ((Func<Task>)(async () => await nodes.GetAsync(id))).Should().ThrowAsync<InvalidOperationException>();
            await ((Func<Task>)(async () => await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=id,ToNodeId=id},Payload("late")))).Should().ThrowAsync<InvalidOperationException>();
            await ((Func<Task>)(async () => await scope.OpenGraphNodeTableAsync(node.Id))).Should().ThrowAsync<InvalidOperationException>();
            await ((Func<Task>)(async () => await scan.MoveNextAsync())).Should().ThrowAsync<InvalidOperationException>();
            await scan.DisposeAsync();
            await engine.ExecuteStatementAsync(async (statement, token) => await (await statement.OpenGraphNodeTableAsync(node.Id, token)).InsertAsync(Payload("next"), token));
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task ActiveJournalRestoresGraphHeapIdentityAdjacencyAndPayloadAfterReopen()
    {
        var path = TempPath("graph-recovery");
        try
        {
            TableId nodesId;
            TableId edgesId;
            GraphNodeId first;
            GraphNodeId second;
            GraphEdgeId edgeId;
            await using (sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                nodesId = (await RegisterNodeAsync(engine,"nodes")).Id;
                edgesId = (await RegisterEdgeAsync(engine,"edges",nodesId)).Id;
                var nodes = await engine.OpenGraphNodeTableAsync(nodesId);
                var edges = await engine.OpenGraphEdgeTableAsync(edgesId);
                first = await nodes.InsertAsync(Payload("first"));
                second = await nodes.InsertAsync(Payload("second"));
                edgeId = await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=second},Payload("before"));
                // Leave the same active journal that interrupted statement execution leaves.
                await using var journal = await sql_storage_engine.Exposures.Transactions.StatementJournalExposure.CreateStatementJournalExposureAsync(path, CancellationToken.None);
                await edges.ReconnectAsync(edgeId,new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=second,ToNodeId=second});
                await edges.UpdateAsync(edgeId,Update("after"));
                await nodes.DeleteAsync(first);
            }

            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            (await (await reopened.OpenGraphNodeTableAsync(nodesId)).GetAsync(first)).Should().NotBeNull();
            var restoredEdges = await reopened.OpenGraphEdgeTableAsync(edgesId);
            var restored = await restoredEdges.GetAsync(edgeId);
            restored!.FromNodeId.Should().Be(first);
            ((TextSqlValue)restored.Row.Values[0]).Value.Should().Be("before");
            (await CollectAsync(restoredEdges.TraverseAsync(first,GraphEdgeDirection.Outgoing))).Should().HaveCount(1);
            (await CollectAsync(restoredEdges.TraverseAsync(second,GraphEdgeDirection.Outgoing))).Should().BeEmpty();
            File.Exists(path + ".statement-undo").Should().BeFalse();
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task WaitingRootHandleAndFailedScopeAreInvalidatedOnRollback()
    {
        var path = TempPath("graph-waiter");
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var definition = await RegisterNodeAsync(engine,"nodes");
            var root = await engine.OpenGraphNodeTableAsync(definition.Id);
            var id = await root.InsertAsync(Payload("original"));
            Task<StoredGraphNode?> waiting = null!;
            IStorageGraphNodeTable scoped = null!;
            await ((Func<Task>)(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                scoped = await statement.OpenGraphNodeTableAsync(definition.Id, token);
                await scoped.UpdateAsync(id, Update("uncommitted"), token);
                waiting = root.GetAsync(id, token).AsTask();
                waiting.IsCompleted.Should().BeFalse();
                throw new InvalidOperationException("abort");
            }))).Should().ThrowAsync<InvalidOperationException>().WithMessage("abort");
            await ((Func<Task>)(async () => await waiting.WaitAsync(TimeSpan.FromSeconds(5)))).Should().ThrowAsync<InvalidOperationException>();
            await ((Func<Task>)(async () => await scoped.GetAsync(id))).Should().ThrowAsync<InvalidOperationException>().WithMessage("*scope has completed*");
            var current = await engine.OpenGraphNodeTableAsync(definition.Id);
            ((TextSqlValue)(await current.GetAsync(id))!.Row.Values[0]).Value.Should().Be("original");
            await engine.DisposeAsync();
            await ((Func<Task>)(async () => await current.GetAsync(id))).Should().ThrowAsync<ObjectDisposedException>();
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task CancelledGraphCallAndEarlyTraversalDisposalDoNotRetainTheGate()
    {
        var path = TempPath("graph-cancel");
        try
        {
            await using sql_storage_engine.Exposures.Storage.StorageSessionExposure engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var node = await RegisterNodeAsync(engine,"nodes");
            var edge = await RegisterEdgeAsync(engine, "edges", node.Id);
            await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var nodes = await statement.OpenGraphNodeTableAsync(node.Id, token);
                var edges = await statement.OpenGraphEdgeTableAsync(edge.Id, token);
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                await ((Func<Task>)(async () => await nodes.InsertAsync(Payload("cancelled"), cancelled.Token))).Should().ThrowAsync<OperationCanceledException>();
                var id = await nodes.InsertAsync(Payload("valid"), token);
                await edges.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=id,ToNodeId=id},Payload("edge"),token);
                await using var traversal = edges.TraverseAsync(id, GraphEdgeDirection.Both, cancellationToken: token).GetAsyncEnumerator(token);
                (await traversal.MoveNextAsync()).Should().BeTrue();
            });
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var nodes = await statement.OpenGraphNodeTableAsync(node.Id, token);
                await using var scan = nodes.ScanAsync(token).GetAsyncEnumerator(token);
                (await scan.MoveNextAsync()).Should().BeTrue();
                (await scan.MoveNextAsync()).Should().BeFalse();
            }, deadline.Token);
        }
        finally
        {
            Delete(path);
        }
    }

    private static Row Payload(string text) => RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(text)]);
    private static RowUpdate Update(string text) => RowUpdateExposure.CreateRowUpdate([new ColumnUpdate { ColumnIndex = 0, Value = sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(text) }]);
    private static async Task<CatalogTable> RegisterNodeAsync(IStorageEngine engine, string name)
    {
        var table = await CreateNodeTableAsync(engine,name);
        var identity = engine.ReadCatalog().GetIndexes(table.Id).Single(index => index.Name == name + "_graph_id");
        return await engine.RegisterGraphNodeTableAsync(table.Id, new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, identity.Id);
    }

    private static async Task<CatalogTable> CreateNodeTableAsync(IStorageEngine engine, string name)
    {
        var table = await engine.CreateCatalogTableAsync(name,[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(100), true), GraphColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"$node_id",CatalogGeneratedAlwaysKind.GraphIdentity)]);
        await engine.CreateCatalogIndexAsync(name + "_graph_id",table.Id,true,[Indexed(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 })]);
        return table;
    }

    private static async Task<CatalogTable> RegisterEdgeAsync(IStorageEngine engine, string name, TableId nodeTableId)
    {
        var table = await engine.CreateCatalogTableAsync(name,[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "payload", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(100), true), GraphColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 },"$edge_id",CatalogGeneratedAlwaysKind.GraphIdentity), GraphColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 3 },"$from_id",CatalogGeneratedAlwaysKind.GraphFromNode), GraphColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 4 },"$to_id",CatalogGeneratedAlwaysKind.GraphToNode)]);
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
        if (File.Exists(path + ".statement-undo"))
            File.Delete(path + ".statement-undo");
    }
}