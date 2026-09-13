using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;

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
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                await engine.ExecuteStatementAsync(async (statement, token) =>
                    nodeId = (await statement.CreateGraphNodeTableAsync(GraphDdlTests.Name("nodes"), GraphDdlTests.Payload, token)).Id);
                using var cancellation = new CancellationTokenSource();
                var reached = false;
                engine.GraphDdlObserver = stage =>
                {
                    if ((int)stage != boundary) return;
                    reached = true;
                    if (cancel) cancellation.Cancel(); else throw new IOException("Injected graph DDL publication failure.");
                };
                Assert.CatchAsync(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
                {
                    try { await statement.CreateGraphEdgeTableAsync(GraphDdlTests.Name("edges"), GraphDdlTests.Payload, nodeId, nodeId, token); }
                    catch (IOException) { }
                    catch (OperationCanceledException) { }
                }, cancellation.Token));
                Assert.That(reached, Is.True);
                Assert.That(engine.Catalog.TryGetTable("edges", out _), Is.False);
                Assert.That(engine.GraphCatalog.Indexes, Has.Count.EqualTo(1));
                engine.GraphDdlObserver = null;
                await engine.ExecuteStatementAsync(async (statement, token) =>
                    await statement.CreateGraphEdgeTableAsync(GraphDdlTests.Name("retry"), GraphDdlTests.Payload, nodeId, nodeId, token));
            }
            await using var reopened = await StorageEngine.OpenAsync(path);
            Assert.That(reopened.Catalog.TryGetTable("edges", out _), Is.False);
            Assert.That(reopened.Catalog.TryGetTable("retry", out var retry), Is.True);
            Assert.That(reopened.Catalog.GetIndexes(retry!.Id), Has.Count.EqualTo(3));
        }
        finally { GraphDdlTests.Delete(path); }
    }
}
