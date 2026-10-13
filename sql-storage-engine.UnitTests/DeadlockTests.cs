using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Exposures.LockResources;

namespace sql_storage_engine.UnitTests;
public sealed class DeadlockTests
{
    private static readonly TableLockResource First = sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateTableLockResource(new sql_storage_engine.Models.LockResources.TableLockResource { TableId = new sql_storage_engine.Models.Identifiers.TableId { Value = 1 } });
    private static readonly TableLockResource Second = sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateTableLockResource(new sql_storage_engine.Models.LockResources.TableLockResource { TableId = new sql_storage_engine.Models.Identifiers.TableId { Value = 2 } });
    private static readonly TableLockResource Third = sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateTableLockResource(new sql_storage_engine.Models.LockResources.TableLockResource { TableId = new sql_storage_engine.Models.Identifiers.TableId { Value = 3 } });
    [Test]
    public async Task TwoTransactionCycle_RollsBackHighestIdAndSurvivorContinues()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        sql_storage_engine.Exposures.Transactions.TransactionManagerExposure transactions = new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure();
        var first = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager);
        var pinsReleased = 0;
        var second = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager, () => pinsReleased++);
        await first.AcquireAsync(First,LockMode.Exclusive);
        await second.AcquireAsync(Second,LockMode.Exclusive);
        var firstWait = first.AcquireAsync(Second,LockMode.Exclusive).AsTask();
        var victimWait = second.AcquireAsync(First,LockMode.Exclusive).AsTask();
        var exception = await ((Func<Task>)(async () => await victimWait)).Should().ThrowAsync<DeadlockException>();
        exception.Which.VictimTransactionId.Should().Be(second.ReadId());
        await firstWait;
        second.ReadState().Should().Be(TransactionState.RolledBack);
        pinsReleased.Should().Be(1);
        manager.ReadDeadlockCount().Should().Be(1);
        manager.ReadLastDeadlockVictim().Should().Be(second.ReadId());
    }

    [Test]
    public async Task ThreeTransactionCycle_RollsBackHighestIdAndOtherTransactionsCanFinish()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        sql_storage_engine.Exposures.Transactions.TransactionManagerExposure transactions = new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure();
        var first = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager);
        var second = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager);
        var third = new sql_storage_engine.Exposures.Transactions.LockingTransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionExposure(transactions.CreateTransactionLifecycle()), manager);
        await first.AcquireAsync(First,LockMode.Exclusive);
        await second.AcquireAsync(Second,LockMode.Exclusive);
        await third.AcquireAsync(Third,LockMode.Exclusive);
        var firstWait = first.AcquireAsync(Second,LockMode.Exclusive).AsTask();
        var secondWait = second.AcquireAsync(Third,LockMode.Exclusive).AsTask();
        var victimWait = third.AcquireAsync(First,LockMode.Exclusive).AsTask();
        await ((Func<Task>)(async () => await victimWait)).Should().ThrowAsync<DeadlockException>();
        await secondWait;
        second.Commit();
        await firstWait;
        manager.ReadLastDeadlockVictim().Should().Be(third.ReadId());
        third.ReadState().Should().Be(TransactionState.RolledBack);
    }

    [Test]
    public async Task NonCyclicWait_IsNotReportedAsDeadlock()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        await manager.AcquireAsync(new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 },First,LockMode.Exclusive);
        var waiting = manager.AcquireAsync(new sql_storage_engine.Models.Identifiers.TransactionId { Value = 2 },First,LockMode.Shared).AsTask();
        waiting.IsCompleted.Should().BeFalse();
        manager.ReadDeadlockCount().Should().Be(0);
        manager.ReadLastDeadlockVictim().Should().BeNull();
        manager.Release(new sql_storage_engine.Models.Identifiers.TransactionId { Value = 1 },First);
        await waiting;
    }
}