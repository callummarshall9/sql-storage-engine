using AwesomeAssertions;
using sql_storage_engine.Diagnostics;

namespace sql_storage_engine.UnitTests;

public sealed class PersistentFormatFuzzTests
{
    [Test]
    public async Task CoreDecoderTargets_RejectRandomInputWithoutUnsafeFailureOrHang()
    {
        var sink = new MemorySink();
        var harness = new PersistentFormatFuzzHarness(sink, TimeSpan.FromSeconds(2));
        var targets = PersistentFormatFuzzHarness.CreateCoreTargets();
        targets.Select(target => target.Name).Should().BeEquivalentTo([
            "database-header", "page-header", "heap-slots", "rows", "keys", "catalog-records",
            "overflow-pages", "index-pages", "wal", "backup-manifests"]);
        foreach (var target in targets)
            (await harness.RunAsync(target, 32, 1729)).Should().BeEmpty($"target {target.Name}");
        sink.Failures.Should().BeEmpty();
    }

    [Test]
    public async Task UnexpectedFailure_IsPersistedAsRegressionFixture()
    {
        var sink = new MemorySink();
        var harness = new PersistentFormatFuzzHarness(sink);
        var target = new FuzzTarget("regression", 16, _ => throw new InvalidOperationException("bug"),
            new ReadOnlyMemory<byte>[] { new byte[] { 1, 2, 3 } });
        var failures = await harness.RunAsync(target, 1, 1);
        failures.Should().ContainSingle().Which.FailureType.Should().Be(nameof(InvalidOperationException));
        sink.Failures.Should().ContainSingle().Which.Input.Should().NotBeNull();
    }

    [Test]
    public async Task OversizedSeeds_NeverCreateInputBeyondTargetAllocationBound()
    {
        var sink = new MemorySink(); var maximumSeen = 0;
        var target = new FuzzTarget("bounded", 8, bytes => maximumSeen = Math.Max(maximumSeen, bytes.Length),
            new ReadOnlyMemory<byte>[] { new byte[1024] });
        await new PersistentFormatFuzzHarness(sink).RunAsync(target, 64, 7);
        maximumSeen.Should().BeLessThanOrEqualTo(8);
    }

    private sealed class MemorySink : IFuzzRegressionSink
    { public List<FuzzFailure> Failures { get; } = []; public void Save(FuzzFailure failure) => Failures.Add(failure); }
}
