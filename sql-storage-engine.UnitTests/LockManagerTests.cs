using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class LockManagerTests
{
    private static readonly TableLockResource Resource = new(new TableId(7));

    [Test]
    public async Task CompatibleLocks_AreGrantedConcurrently()
    {
        var manager = new LockManager();
        await manager.AcquireAsync(Tx(1), Resource, LockMode.Update);
        var shared = manager.AcquireAsync(Tx(2), Resource, LockMode.Shared).AsTask();
        shared.IsCompletedSuccessfully.Should().BeTrue();
        await shared;
    }

    [Test]
    public async Task IncompatibleLocks_WaitAndFifoPreventsBarging()
    {
        var manager = new LockManager();
        await manager.AcquireAsync(Tx(1), Resource, LockMode.Shared);
        var exclusive = manager.AcquireAsync(Tx(2), Resource, LockMode.Exclusive).AsTask();
        var laterShared = manager.AcquireAsync(Tx(3), Resource, LockMode.Shared).AsTask();
        exclusive.IsCompleted.Should().BeFalse();
        laterShared.IsCompleted.Should().BeFalse();

        manager.Release(Tx(1), Resource).Should().BeTrue();
        await exclusive;
        laterShared.IsCompleted.Should().BeFalse();
        manager.Release(Tx(2), Resource).Should().BeTrue();
        await laterShared;
    }

    [Test]
    public async Task Cancellation_RemovesWaiterAndUnblocksLaterCompatibleRequest()
    {
        var manager = new LockManager();
        await manager.AcquireAsync(Tx(1), Resource, LockMode.Shared);
        using var cancellation = new CancellationTokenSource();
        var blocked = manager.AcquireAsync(Tx(2), Resource, LockMode.Exclusive, cancellation.Token).AsTask();
        var later = manager.AcquireAsync(Tx(3), Resource, LockMode.Shared).AsTask();
        cancellation.Cancel();

        await ((Func<Task>)(async () => await blocked)).Should().ThrowAsync<OperationCanceledException>();
        await later;
    }

    [Test]
    public async Task Conversion_WaitsInFifoOrderAndRetainsExistingLock()
    {
        var manager = new LockManager();
        await manager.AcquireAsync(Tx(1), Resource, LockMode.Update);
        await manager.AcquireAsync(Tx(2), Resource, LockMode.Shared);
        var conversion = manager.ConvertAsync(Tx(1), Resource, LockMode.Exclusive).AsTask();
        var later = manager.AcquireAsync(Tx(3), Resource, LockMode.Shared).AsTask();
        conversion.IsCompleted.Should().BeFalse();
        later.IsCompleted.Should().BeFalse();

        manager.Release(Tx(2), Resource);
        await conversion;
        manager.Release(Tx(1), Resource);
        await later;
    }

    [TestCase(TransactionState.Committed)]
    [TestCase(TransactionState.RolledBack)]
    [TestCase(TransactionState.Failed)]
    public async Task EveryTerminalPath_ReleasesEveryOwnedLock(TransactionState outcome)
    {
        var manager = new LockManager();
        var transaction = outcome == TransactionState.Failed
            ? new TransactionManager().Begin(commit: () => throw new IOException("failure"))
            : new TransactionManager().Begin();
        var locking = new LockingTransaction(transaction, manager);
        await locking.AcquireAsync(Resource, LockMode.Exclusive);
        await locking.AcquireAsync(new TableLockResource(new TableId(8)), LockMode.Exclusive);
        var waiting = manager.AcquireAsync(Tx(99), Resource, LockMode.Exclusive).AsTask();

        if (outcome == TransactionState.Committed) locking.Commit();
        else if (outcome == TransactionState.RolledBack) locking.Rollback();
        else ((Action)locking.Commit).Should().Throw<IOException>();
        await waiting;
    }

    [Test]
    public async Task Disposal_ReleasesEveryOwnedLock()
    {
        var manager = new LockManager();
        var locking = new LockingTransaction(new TransactionManager().Begin(), manager);
        await locking.AcquireAsync(Resource, LockMode.Exclusive);
        var waiting = manager.AcquireAsync(Tx(2), Resource, LockMode.Exclusive).AsTask();
        locking.Dispose();
        await waiting;
    }

    [Test]
    public async Task ConcurrentIndependentResources_PreserveLockState()
    {
        var manager = new LockManager();
        const int count = 32;
        await Task.WhenAll(Enumerable.Range(1, count).Select(async value =>
        {
            var resource = new TableLockResource(new TableId((ulong)value));
            await manager.AcquireAsync(Tx(value), resource, LockMode.Exclusive);
            manager.Release(Tx(value), resource).Should().BeTrue();
            manager.Release(Tx(value), resource).Should().BeFalse();
        }));

        await manager.AcquireAsync(Tx(100), Resource, LockMode.Exclusive);
        manager.Release(Tx(100), Resource).Should().BeTrue();
    }

    private static TransactionId Tx(int value) => new((ulong)value);
}
