using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Exposures.LockResources;

namespace sql_storage_engine.UnitTests;
public sealed class LockManagerTests
{
    private static readonly TableLockResource Resource = sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateTableLockResource(new sql_storage_engine.Models.LockResources.TableLockResource { TableId = new sql_storage_engine.Models.Identifiers.TableId { Value = 7 } });
    [Test]
    public async Task CompatibleLocks_AreGrantedConcurrently()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        await manager.AcquireAsync(Tx(1),Resource,LockMode.Update);
        var shared = manager.AcquireAsync(Tx(2),Resource,LockMode.Shared).AsTask();
        shared.IsCompletedSuccessfully.Should().BeTrue();
        await shared;
    }

    [Test]
    public async Task IncompatibleLocks_WaitAndFifoPreventsBarging()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        await manager.AcquireAsync(Tx(1),Resource,LockMode.Shared);
        var exclusive = manager.AcquireAsync(Tx(2),Resource,LockMode.Exclusive).AsTask();
        var laterShared = manager.AcquireAsync(Tx(3),Resource,LockMode.Shared).AsTask();
        exclusive.IsCompleted.Should().BeFalse();
        laterShared.IsCompleted.Should().BeFalse();
        manager.Release(Tx(1),Resource).Should().BeTrue();
        await exclusive;
        laterShared.IsCompleted.Should().BeFalse();
        manager.Release(Tx(2),Resource).Should().BeTrue();
        await laterShared;
    }

    [Test]
    public async Task Cancellation_RemovesWaiterAndUnblocksLaterCompatibleRequest()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        await manager.AcquireAsync(Tx(1),Resource,LockMode.Shared);
        using var cancellation = new CancellationTokenSource();
        var blocked = manager.AcquireAsync(Tx(2),Resource,LockMode.Exclusive,cancellation.Token).AsTask();
        var later = manager.AcquireAsync(Tx(3),Resource,LockMode.Shared).AsTask();
        cancellation.Cancel();
        await ((Func<Task>)(async () => await blocked)).Should().ThrowAsync<OperationCanceledException>();
        await later;
    }

    [Test]
    public async Task Conversion_WaitsInFifoOrderAndRetainsExistingLock()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        await manager.AcquireAsync(Tx(1),Resource,LockMode.Update);
        await manager.AcquireAsync(Tx(2),Resource,LockMode.Shared);
        var conversion = manager.ConvertAsync(Tx(1),Resource,LockMode.Exclusive).AsTask();
        var later = manager.AcquireAsync(Tx(3),Resource,LockMode.Shared).AsTask();
        conversion.IsCompleted.Should().BeFalse();
        later.IsCompleted.Should().BeFalse();
        manager.Release(Tx(2),Resource);
        await conversion;
        manager.Release(Tx(1),Resource);
        await later;
    }

    [TestCase(TransactionState.Committed)]
    [TestCase(TransactionState.RolledBack)]
    [TestCase(TransactionState.Failed)]
    public async Task EveryTerminalPath_ReleasesEveryOwnedLock(TransactionState outcome)
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        sql_storage_engine.Exposures.Transactions.TransactionExposure transaction = outcome == TransactionState.Failed ? new sql_storage_engine.Exposures.Transactions.TransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure().CreateTransactionLifecycle(commit: () => throw new IOException("failure"))) : new sql_storage_engine.Exposures.Transactions.TransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure().CreateTransactionLifecycle());
        var locking = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(transaction, manager);
        await locking.AcquireAsync(Resource,LockMode.Exclusive);
        await locking.AcquireAsync(sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateTableLockResource(new sql_storage_engine.Models.LockResources.TableLockResource { TableId = new sql_storage_engine.Models.Identifiers.TableId { Value = 8 } }),LockMode.Exclusive);
        var waiting = manager.AcquireAsync(Tx(99),Resource,LockMode.Exclusive).AsTask();
        if (outcome == TransactionState.Committed)
            locking.Commit();
        else if (outcome == TransactionState.RolledBack)
            locking.Rollback();
        else
            ((Action)locking.Commit).Should().Throw<IOException>();
        await waiting;
    }

    [Test]
    public async Task Disposal_ReleasesEveryOwnedLock()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        var locking = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure().CreateTransactionLifecycle()), manager);
        await locking.AcquireAsync(Resource,LockMode.Exclusive);
        var waiting = manager.AcquireAsync(Tx(2),Resource,LockMode.Exclusive).AsTask();
        locking.Dispose();
        await waiting;
    }

    [Test]
    public async Task ConcurrentIndependentResources_PreserveLockState()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        const int count = 32;
        await Task.WhenAll(Enumerable.Range(1, count).Select(async value =>
        {
            var resource = sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateTableLockResource(new sql_storage_engine.Models.LockResources.TableLockResource { TableId = new sql_storage_engine.Models.Identifiers.TableId { Value = (ulong)value } });
            await manager.AcquireAsync(Tx(value),resource,LockMode.Exclusive);
            manager.Release(Tx(value),resource).Should().BeTrue();
            manager.Release(Tx(value),resource).Should().BeFalse();
        }));
        await manager.AcquireAsync(Tx(100),Resource,LockMode.Exclusive);
        manager.Release(Tx(100),Resource).Should().BeTrue();
    }

    private static TransactionId Tx(int value) => new sql_storage_engine.Models.Identifiers.TransactionId
    {
        Value = (ulong)value
    };
}