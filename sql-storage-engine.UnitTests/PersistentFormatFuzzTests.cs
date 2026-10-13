// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.UnitTests;
public sealed class PersistentFormatFuzzTests
{
    [Test]
    public async Task CoreDecoderTargets_RejectRandomInputWithoutUnsafeFailureOrHang()
    {
        var sink = new MemorySink();
        var harness = new PersistentFormatFuzzHarnessExposure(sink, TimeSpan.FromSeconds(2));
        var targets = new FuzzCoreTargetsExposure().ReadTargets();
        targets.Select(target => target.Name).Should().BeEquivalentTo(["database-header", "page-header", "heap-slots", "rows", "keys", "catalog-records", "overflow-pages", "index-pages", "wal", "backup-manifests"]);
        foreach (var target in targets)
            (await harness.RunAsync(target,32,1729)).Should().BeEmpty($"target {target.Name}");
        sink.Failures.Should().BeEmpty();
    }

    [Test]
    public async Task UnexpectedFailure_IsPersistedAsRegressionFixture()
    {
        var sink = new MemorySink();
        var harness = new PersistentFormatFuzzHarnessExposure(sink);
        var target = new FuzzTarget
        {
            Name = "regression",
            MaximumInputBytes = 16,
            Decode = _ => throw new InvalidOperationException("bug"),
            GoldenSeeds = new ReadOnlyMemory<byte>[]
            {
                new byte[]
                {
                    1,
                    2,
                    3
                }
            }
        };
        var failures = await harness.RunAsync(target,1,1);
        failures.Should().ContainSingle().Which.FailureType.Should().Be(nameof(InvalidOperationException));
        sink.Failures.Should().ContainSingle().Which.Input.Should().NotBeNull();
    }

    [Test]
    public async Task OversizedSeeds_NeverCreateInputBeyondTargetAllocationBound()
    {
        var sink = new MemorySink();
        var maximumSeen = 0;
        var target = new FuzzTarget
        {
            Name = "bounded",
            MaximumInputBytes = 8,
            Decode = bytes => maximumSeen = Math.Max(maximumSeen, bytes.Length),
            GoldenSeeds = new ReadOnlyMemory<byte>[]
            {
                new byte[1024]
            }
        };
        await new PersistentFormatFuzzHarnessExposure(sink).RunAsync(target,64,7);
        maximumSeen.Should().BeLessThanOrEqualTo(8);
    }

    [Test]
    public async Task TimedOutDecoder_IsRecordedAndReleasedAfterExecutionFinishes()
    {
        using var release = new ManualResetEventSlim(false);
        var sink = new MemorySink();
        var target = new FuzzTarget
        {
            Name = "timeout",
            MaximumInputBytes = 8,
            Decode = _ => release.Wait(),
            GoldenSeeds = new ReadOnlyMemory<byte>[] { new byte[] { 1 } }
        };
        try
        {
            var failures = await new PersistentFormatFuzzHarnessExposure(sink, TimeSpan.FromMilliseconds(20)).RunAsync(target, 1, 7);
            failures.Should().ContainSingle().Which.FailureType.Should().Be("TIMEOUT");
            sink.Failures.Should().ContainSingle();
        }
        finally
        {
            release.Set();
        }
    }

    [Test]
    public async Task CancellationDuringDecode_DoesNotSaveTimeoutAsRegression()
    {
        using var release = new ManualResetEventSlim(false);
        using var cancellation = new CancellationTokenSource();
        var sink = new MemorySink();
        var target = new FuzzTarget
        {
            Name = "cancelled",
            MaximumInputBytes = 8,
            Decode = _ => { cancellation.Cancel(); release.Wait(); },
            GoldenSeeds = new ReadOnlyMemory<byte>[] { new byte[] { 1 } }
        };
        try
        {
            await ((Func<Task>)(async () => await new PersistentFormatFuzzHarnessExposure(sink).RunAsync(target, 1, 7, cancellation.Token))).Should().ThrowAsync<OperationCanceledException>();
            sink.Failures.Should().BeEmpty();
        }
        finally
        {
            release.Set();
        }
    }

    private sealed class MemorySink : IFuzzRegressionSinkExposure
    {
        public List<FuzzFailure> Failures { get; } = [];

        public void Save(FuzzFailure failure) => Failures.Add(failure);
    }
}