using AwesomeAssertions;
using sql_storage_engine.Exposures.Retention;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class CoordinationRetentionArchitectureTests
{
    [Test]
    public void ConcurrentRetentionReleaseKeepsOtherRegistrationAndMinimum()
    {
        var retention = new WalRetentionExposure();
        var first = retention.Register(new LogSequenceNumber { Value = 3 });
        using var second = retention.Register(new LogSequenceNumber { Value = 7 });
        retention.ReadMinimumRetainedLsn()!.Value.Value.Should().Be(3);
        Parallel.For(0, 64, _ => first.Dispose());
        retention.ReadActiveCount().Should().Be(1);
        retention.ReadMinimumRetainedLsn()!.Value.Value.Should().Be(7);
        ((Action)(() => ((WalRetentionExposure)first).Register(new LogSequenceNumber { Value = 9 }))).Should().Throw<ObjectDisposedException>();
        ((Action)(() => retention.Register(default))).Should().Throw<ArgumentOutOfRangeException>().WithParameterName("logSequenceNumber");
    }

    [Test]
    public async Task ConcurrentReaderReleaseDoesNotReleaseAnotherReader()
    {
        using var coordinator = new TransactionCoordinatorExposure();
        var first = await coordinator.AcquireAsync(TransactionAccess.Read);
        var second = await coordinator.AcquireAsync(TransactionAccess.Read);
        var writer = coordinator.AcquireAsync(TransactionAccess.Write).AsTask();
        Parallel.For(0, 64, _ => first.Dispose());
        writer.IsCompleted.Should().BeFalse();
        second.Dispose();
        using var writerLease = await writer.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task CancellationRestoresCoordinationAndPreservesItsToken()
    {
        using var coordinator = new TransactionCoordinatorExposure();
        var reader = await coordinator.AcquireAsync(TransactionAccess.Read);
        using var cancellation = new CancellationTokenSource();
        var waiting = coordinator.AcquireAsync(TransactionAccess.Write, cancellation.Token).AsTask();
        cancellation.Cancel();
        var failure = await ((Func<Task>)(async () => await waiting)).Should().ThrowAsync<OperationCanceledException>();
        failure.Which.CancellationToken.Should().Be(cancellation.Token);
        reader.Dispose();
        using var writer = await coordinator.AcquireAsync(TransactionAccess.Write).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Test]
    public async Task CloseRejectsOutstandingLeasesAndReleasedHandlesCannotAcquire()
    {
        var coordinator = new TransactionCoordinatorExposure();
        var lease = await coordinator.AcquireAsync(TransactionAccess.Read);
        ((Action)coordinator.Dispose).Should().Throw<InvalidOperationException>();
        lease.Dispose();
        await ((Func<Task>)(async () => await ((TransactionCoordinatorExposure)lease).AcquireAsync(TransactionAccess.Read))).Should().ThrowAsync<ObjectDisposedException>();
        coordinator.Dispose();
        coordinator.Dispose();
        await ((Func<Task>)(async () => await coordinator.AcquireAsync(TransactionAccess.Read))).Should().ThrowAsync<ObjectDisposedException>();
    }
}