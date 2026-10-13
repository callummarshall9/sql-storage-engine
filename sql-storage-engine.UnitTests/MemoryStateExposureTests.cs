using AwesomeAssertions;
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Exceptions;

namespace sql_storage_engine.UnitTests;
public sealed class MemoryStateExposureTests
{
    [Test]
    public async Task RecoveryFlagIsMonotonicAndIndependentAcrossInstances()
    {
        var recovery = new RecoveryRequirementExposure();
        var other = new RecoveryRequirementExposure();
        recovery.IsRecoveryRequired().Should().BeFalse();
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(recovery.MarkRecoveryRequired)));
        recovery.IsRecoveryRequired().Should().BeTrue();
        other.IsRecoveryRequired().Should().BeFalse();
    }

    [TestCase(-1, 0L, 0L, "NextPageIndex")]
    [TestCase(0, -1L, 0L, "ReclaimedBytes")]
    [TestCase(0, 0L, -1L, "FailureCount")]
    public async Task InvalidCheckpointDoesNotReplacePublishedProgress(int nextPageIndex, long reclaimedBytes, long failureCount, string parameterName)
    {
        var checkpoint = new MemoryHeapMaintenanceCheckpointExposure();
        var published = new HeapMaintenanceProgress
        {
            NextPageIndex = 3,
            ReclaimedBytes = 42,
            FailureCount = 1
        };
        await checkpoint.WriteAsync(published);
        var invalid = new HeapMaintenanceProgress
        {
            NextPageIndex = nextPageIndex,
            ReclaimedBytes = reclaimedBytes,
            FailureCount = failureCount
        };
        var failure = await ((Func<Task>)(async () => await checkpoint.WriteAsync(invalid))).Should().ThrowAsync<StorageModelValidationException>();
        failure.Which.InnerException.Should().BeOfType<ArgumentOutOfRangeException>().Which.ParamName.Should().Be(parameterName);
        (await checkpoint.ReadAsync()).Should().BeSameAs(published);
    }

    [Test]
    public async Task CancelledCheckpointOperationsPreserveTokenAndPublishedProgress()
    {
        var checkpoint = new MemoryHeapMaintenanceCheckpointExposure();
        var published = new HeapMaintenanceProgress
        {
            NextPageIndex = 2
        };
        await checkpoint.WriteAsync(published);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var write = await ((Func<Task>)(async () => await checkpoint.WriteAsync(new HeapMaintenanceProgress(), cancellation.Token))).Should().ThrowAsync<OperationCanceledException>();
        write.Which.CancellationToken.Should().Be(cancellation.Token);
        write.Which.InnerException.Should().BeOfType<OperationCanceledException>();
        var read = await ((Func<Task>)(async () => await checkpoint.ReadAsync(cancellation.Token))).Should().ThrowAsync<OperationCanceledException>();
        read.Which.CancellationToken.Should().Be(cancellation.Token);
        (await checkpoint.ReadAsync()).Should().BeSameAs(published);
    }
}