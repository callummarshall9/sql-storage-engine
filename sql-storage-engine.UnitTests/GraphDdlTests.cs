using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

using sql_storage_engine.Models.Graph;

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
            await using (var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                await engine.ExecuteStatementAsync(async (statement, token) =>
                {
                    var nodes = await statement.CreateCatalogTableGraphNodeAsync(Name("nodes"),Payload,token);
                    var edges = await statement.CreateCatalogTableGraphEdgeAsync(Name("edges"),Payload,new sql_storage_engine.Models.Graph.GraphTableEndpoints {FromNodeTableId=nodes.Id,ToNodeTableId=nodes.Id},token);
                    nodesId = nodes.Id;
                    edgesId = edges.Id;
                    Assert.That(nodes.Columns[^1].IsHidden, Is.True);
                    Assert.That(edges.Columns.TakeLast(3).All(column => column.IsHidden), Is.True);
                    Assert.That(engine.ReadCatalog().GetIndexes(nodes.Id), Has.Count.EqualTo(1));
                    Assert.That(engine.ReadCatalog().GetIndexes(edges.Id), Has.Count.EqualTo(3));
                    var nodeTable = await statement.OpenGraphNodeTableAsync(nodes.Id, token);
                    nodeId = await nodeTable.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]), token);
                    edgeId = await (await statement.OpenGraphEdgeTableAsync(edges.Id, token)).InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=nodeId,ToNodeId=nodeId},RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]),token);
                });
            }

            await using var reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            var nodeHandle = await reopened.OpenGraphNodeTableAsync(nodesId);
            Assert.That(await nodeHandle.GetAsync(nodeId), Is.Not.Null);
            Assert.That(await (await reopened.OpenGraphEdgeTableAsync(edgesId)).GetAsync(edgeId), Is.Not.Null);
            Assert.ThrowsAsync<GraphReferentialIntegrityException>(async () => await nodeHandle.DeleteAsync(nodeId));
        }
        finally
        {
            Delete(path);
        }
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
            await using var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            var ordinary = await engine.CreateCatalogTableAsync("existing", Payload);
            await (await engine.OpenTableAsync(ordinary.Id)).InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(9)]));
            Assert.CatchAsync(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var node = await statement.CreateCatalogTableGraphNodeAsync(Name("temporary"),Payload,token);
                if (failure == "unknown" || failure == "ordinary")
                    await statement.CreateCatalogTableGraphEdgeAsync(Name("bad"),Payload,new sql_storage_engine.Models.Graph.GraphTableEndpoints {FromNodeTableId=failure == "unknown" ? new sql_storage_engine.Models.Identifiers.TableId { Value = 999 } : ordinary.Id,ToNodeTableId=node.Id},token);
                else if (failure == "reserved")
                    await statement.CreateCatalogTableGraphNodeAsync(Name("bad"),[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "$node_id", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)],token);
                else
                    await statement.CreateCatalogTableGraphNodeAsync(Name(failure == "populated" ? "existing" : "temporary"),Payload,token);
            }));
            Assert.That(engine.ReadCatalog().TryGetTable("temporary", out _), Is.False);
            Assert.That(engine.ReadCatalog().TryGetTable("bad", out _), Is.False);
            Assert.That(engine.ReadCatalog().TryGetTable(ordinary.Id, out var restored), Is.True);
            Assert.That(restored!.Graph, Is.Null);
            await engine.ExecuteStatementAsync(async (statement, token) => await statement.CreateCatalogTableGraphNodeAsync(Name("retry"),Payload,token));
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task CompletedAndFailedScopesCannotCreateTables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"graph-ddl-{Guid.NewGuid():N}.db");
        try
        {
            await using var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            IStorageStatement? escaped = null;
            await engine.ExecuteStatementAsync((statement, _) =>
            {
                escaped = statement;
                return ValueTask.CompletedTask;
            });
            Assert.ThrowsAsync<InvalidOperationException>(async () => await escaped!.CreateCatalogTableGraphNodeAsync(Name("late"),Payload));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                escaped = statement;
                try
                {
                    await statement.CreateCatalogTableGraphEdgeAsync(Name("invalid"),Payload,new sql_storage_engine.Models.Graph.GraphTableEndpoints {FromNodeTableId=new sql_storage_engine.Models.Identifiers.TableId { Value = 999 },ToNodeTableId=new sql_storage_engine.Models.Identifiers.TableId { Value = 999 }},token);
                }
                catch (ArgumentException)
                {
                }
            }));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await escaped!.CreateCatalogTableGraphNodeAsync(Name("late"),Payload));
            Assert.That(engine.ReadCatalog().TryGetTable("late", out _), Is.False);
        }
        finally
        {
            Delete(path);
        }
    }

    [Test]
    public async Task DistinctEndpointRolesRejectReversedPairAndOrdinaryMutationOfGeneratedColumns()
    {
        var path = Path.Combine(Path.GetTempPath(), $"graph-ddl-pair-{Guid.NewGuid():N}.db");
        try
        {
            await using var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path);
            await engine.ExecuteStatementAsync(async (statement, token) =>
            {
                var from = await statement.CreateCatalogTableGraphNodeAsync(Name("from_nodes"),Payload,token);
                var to = await statement.CreateCatalogTableGraphNodeAsync(Name("to_nodes"),Payload,token);
                var edges = await statement.CreateCatalogTableGraphEdgeAsync(Name("edges"),Payload,new sql_storage_engine.Models.Graph.GraphTableEndpoints {FromNodeTableId=from.Id,ToNodeTableId=to.Id},token);
                var first = await (await statement.OpenGraphNodeTableAsync(from.Id, token)).InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(1)]), token);
                var second = await (await statement.OpenGraphNodeTableAsync(to.Id, token)).InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(2)]), token);
                var handle = await statement.OpenGraphEdgeTableAsync(edges.Id, token);
                await handle.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=first,ToNodeId=second},RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(3)]),token);
                Assert.ThrowsAsync<ArgumentException>(async () => await handle.InsertAsync(new sql_storage_engine.Models.Graph.GraphEndpoints {FromNodeId=second,ToNodeId=first},RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(4)]),token));
                var ordinary = await statement.OpenTableAsync(edges.Id, token);
                Assert.ThrowsAsync<InvalidOperationException>(async () => await ordinary.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(5)]), token));
            });
        }
        finally
        {
            Delete(path);
        }
    }

    internal static CatalogTableName Name(string name) => sql_storage_engine.Exposures.Catalog.CatalogTableNameExposure.CreateCatalogTableName("default","dbo",name);
    internal static CatalogColumn[] Payload => [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)];

    internal static void Delete(string path)
    {
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*"))
            File.Delete(file);
    }
}