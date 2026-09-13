using sql_storage_engine.Identifiers;

namespace sql_storage_engine.UnitTests;

public sealed class GraphDdlRecoveryTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public async Task InterruptedPublicationImagesRecoverWithoutPartialTablesOrIndexes(int boundary)
    {
        var path = Path.Combine(Path.GetTempPath(), $"graph-ddl-recovery-{Guid.NewGuid():N}.db");
        var crash = path + ".crash";
        try
        {
            TableId nodeId = default;
            await using (var engine = await StorageEngine.CreateAsync(path))
            {
                await engine.ExecuteStatementAsync(async (statement, token) =>
                    nodeId = (await statement.CreateGraphNodeTableAsync(GraphDdlTests.Name("nodes"), GraphDdlTests.Payload, token)).Id);
                engine.GraphDdlObserver = stage =>
                {
                    if ((int)stage != boundary) return;
                    // Capture the persisted database and still-active before-image journal at this exact boundary.
                    File.Copy(path, crash);
                    File.Copy(path + ".statement-undo", crash + ".statement-undo");
                    throw new IOException("Simulated interruption after capturing the durable image.");
                };
                Assert.ThrowsAsync<IOException>(async () => await engine.ExecuteStatementAsync(async (statement, token) =>
                    await statement.CreateGraphEdgeTableAsync(GraphDdlTests.Name("edges"), GraphDdlTests.Payload, nodeId, nodeId, token)));
            }
            await using var recovered = await StorageEngine.OpenAsync(crash);
            Assert.That(recovered.Catalog.TryGetTable("edges", out _), Is.False);
            Assert.That(recovered.Catalog.TryGetTable(nodeId, out _), Is.True);
            Assert.That(recovered.GraphCatalog.Indexes, Has.Count.EqualTo(1));
            Assert.That(File.Exists(crash + ".statement-undo"), Is.False);
            await recovered.ExecuteStatementAsync(async (statement, token) =>
                await statement.CreateGraphEdgeTableAsync(GraphDdlTests.Name("edges"), GraphDdlTests.Payload, nodeId, nodeId, token));
            Assert.That(recovered.Catalog.TryGetTable("edges", out var edges), Is.True);
            Assert.That(recovered.Catalog.GetIndexes(edges!.Id), Has.Count.EqualTo(3));
        }
        finally { GraphDdlTests.Delete(path); }
    }
}
