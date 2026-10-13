using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;

namespace sql_storage_engine.UnitTests;
public sealed class GraphDdlFailureTests
{
    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(4, false)]
    [TestCase(0, true)]
    [TestCase(1, true)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    public async Task FailureAtEveryPublicationBoundaryRollsBackEvenWhenCallbackCatchesIt(int boundary, bool cancel)
    {
        var path = Path.Combine(Path.GetTempPath(), $"graph-ddl-fault-{Guid.NewGuid():N}.db");
        try
        {
            TableId nodeId = default;
            await using (var engine = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.CreateStorageSessionExposureAsync(path))
            {
                await engine.ExecuteStatementAsync(async (statement, token) => nodeId = (await statement.CreateCatalogTableGraphNodeAsync(GraphDdlTests.Name("nodes"),GraphDdlTests.Payload,token)).Id);
                using var cancellation = new CancellationTokenSource();
                var reached = false;
                engine.WriteGraphDdlObserver(observer:stage =>
                {
                    if ((int)stage != boundary)
                        return;
                    reached = true;
                    if (cancel)
                        cancellation.Cancel();
                    else
                        throw new IOException("Injected graph DDL publication failure.");
                });
                Assert.CatchAsync(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
                {
                    try
                    {
                        await statement.CreateCatalogTableGraphEdgeAsync(GraphDdlTests.Name("edges"),GraphDdlTests.Payload,new sql_storage_engine.Models.Graph.GraphTableEndpoints {FromNodeTableId=nodeId,ToNodeTableId=nodeId},token);
                    }
                    catch (IOException)
                    {
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }, cancellation.Token));
                Assert.That(reached, Is.True);
                Assert.That(engine.ReadCatalog().TryGetTable("edges", out _), Is.False);
                Assert.That(engine.ReadCatalog().ReadIndexes(), Has.Count.EqualTo(1));
                engine.WriteGraphDdlObserver(observer:null);
                await engine.ExecuteStatementAsync(async (statement, token) => await statement.CreateCatalogTableGraphEdgeAsync(GraphDdlTests.Name("retry"),GraphDdlTests.Payload,new sql_storage_engine.Models.Graph.GraphTableEndpoints {FromNodeTableId=nodeId,ToNodeTableId=nodeId},token));
            }

            await using var reopened = await sql_storage_engine.Exposures.Storage.StorageSessionExposure.OpenStorageSessionExposureAsync(path);
            Assert.That(reopened.ReadCatalog().TryGetTable("edges", out _), Is.False);
            Assert.That(reopened.ReadCatalog().TryGetTable("retry", out var retry), Is.True);
            Assert.That(reopened.ReadCatalog().GetIndexes(retry!.Id), Has.Count.EqualTo(3));
        }
        finally
        {
            GraphDdlTests.Delete(path);
        }
    }
}