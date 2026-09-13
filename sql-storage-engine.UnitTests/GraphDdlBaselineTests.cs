using System.Diagnostics;
using System.Text.Json;

namespace sql_storage_engine.UnitTests;

public sealed class GraphDdlBaselineTests
{
    [Test]
    public async Task RecordsAtomicCreationCostAndChecksEveryIndex()
    {
        var measurements = new List<object>();
        foreach (var count in new[] { 1, 5, 10 })
        {
            var path = Path.Combine(Path.GetTempPath(), $"graph-ddl-baseline-{Guid.NewGuid():N}.db");
            try
            {
                await using var engine = await StorageEngine.CreateAsync(path);
                var before = GC.GetTotalAllocatedBytes(true);
                var started = Stopwatch.GetTimestamp();
                for (var i = 0; i < count; i++)
                    await engine.ExecuteStatementAsync(async (statement, token) =>
                    {
                        var node = await statement.CreateGraphNodeTableAsync(GraphDdlTests.Name($"nodes{i}"), GraphDdlTests.Payload, token);
                        var edge = await statement.CreateGraphEdgeTableAsync(GraphDdlTests.Name($"edges{i}"), GraphDdlTests.Payload, node.Id, node.Id, token);
                        Assert.That(engine.Catalog.GetIndexes(edge.Id), Has.Count.EqualTo(3));
                    });
                var ticks = Stopwatch.GetTimestamp() - started;
                var allocatedBytes = GC.GetTotalAllocatedBytes(true) - before;
                Assert.That(engine.GraphCatalog.Tables, Has.Count.EqualTo(count * 2));
                Assert.That(engine.GraphCatalog.Indexes, Has.Count.EqualTo(count * 4));
                measurements.Add(new { count, ticks, allocatedBytes, fileBytes = new FileInfo(path).Length });
            }
            finally { GraphDdlTests.Delete(path); }
        }
        if (Environment.GetEnvironmentVariable("STORAGE112_BASELINE_OUTPUT") is { Length: > 0 } output)
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
            {
                version = "1.12.0",
                runtime = Environment.Version.ToString(),
                timestampFrequency = Stopwatch.Frequency,
                notes = "1/5/10 node-and-edge pairs, each pair created in one durable statement; total process allocations and database bytes. Host-specific observations, no timing threshold or comparison claim.",
                measurements
            }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }
}
