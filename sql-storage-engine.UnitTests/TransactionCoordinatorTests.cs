using AwesomeAssertions;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class TransactionCoordinatorTests
{
    [Test]
    public async Task MultipleReaders_ProceedConcurrently()
    {
        var coordinator = new TransactionCoordinator();
        using var first = await coordinator.AcquireAsync(TransactionAccess.Read);
        var second = coordinator.AcquireAsync(TransactionAccess.Read).AsTask();
        (await Task.WhenAny(second, Task.Delay(1000))).Should().Be(second);
        using var secondLease = await second;
    }

    [Test]
    public async Task OnlyOneWriterBecomesActiveAndWaitingAcquisitionSupportsCancellation()
    {
        var coordinator = new TransactionCoordinator();
        using var writer = await coordinator.AcquireAsync(TransactionAccess.Write);
        using var cancellation = new CancellationTokenSource();
        var waiting = coordinator.AcquireAsync(TransactionAccess.Write, cancellation.Token).AsTask();
        cancellation.Cancel();
        await ((Func<Task>)(async () => await waiting)).Should().ThrowAsync<OperationCanceledException>();
    }

    [TestCase(TransactionState.Committed)]
    [TestCase(TransactionState.RolledBack)]
    [TestCase(TransactionState.Failed)]
    public async Task EveryTerminalPath_ReleasesWriterLock(TransactionState outcome)
    {
        var coordinator = new TransactionCoordinator();
        var lease = await coordinator.AcquireAsync(TransactionAccess.Write);
        var transaction = outcome == TransactionState.Failed
            ? new TransactionManager().Begin(commit: () => throw new IOException("failure"))
            : new TransactionManager().Begin();
        var coordinated = new CoordinatedTransaction(transaction, lease);
        if (outcome == TransactionState.Committed) coordinated.Commit();
        else if (outcome == TransactionState.RolledBack) coordinated.Rollback();
        else ((Action)coordinated.Commit).Should().Throw<IOException>();
        using var next = await coordinator.AcquireAsync(TransactionAccess.Write);
    }

    [Test]
    public async Task ReaderCannotObserveHalfCompletedWriterState()
    {
        var coordinator = new TransactionCoordinator();
        var committed = 1;
        using var writer = await coordinator.AcquireAsync(TransactionAccess.Write);
        var read = Task.Run(async () => { using var lease = await coordinator.AcquireAsync(TransactionAccess.Read); return committed; });
        committed = 2;
        await Task.Delay(20);
        read.IsCompleted.Should().BeFalse();
        writer.Dispose();
        (await read).Should().Be(2);
    }
}
