using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class DeadlockTests
{
    private static readonly TableLockResource First = new(new TableId(1));
    private static readonly TableLockResource Second = new(new TableId(2));
    private static readonly TableLockResource Third = new(new TableId(3));

    [Test]
    public async Task TwoTransactionCycle_RollsBackHighestIdAndSurvivorContinues()
    {
        var manager = new LockManager();
        var transactions = new TransactionManager();
        var first = new LockingTransaction(transactions.Begin(), manager);
        var pinsReleased = 0;
        var second = new LockingTransaction(transactions.Begin(), manager, () => pinsReleased++);
        await first.AcquireAsync(First, LockMode.Exclusive);
        await second.AcquireAsync(Second, LockMode.Exclusive);
        var firstWait = first.AcquireAsync(Second, LockMode.Exclusive).AsTask();

        var victimWait = second.AcquireAsync(First, LockMode.Exclusive).AsTask();

        var exception = await ((Func<Task>)(async () => await victimWait)).Should().ThrowAsync<DeadlockException>();
        exception.Which.VictimTransactionId.Should().Be(second.Transaction.Id);
        await firstWait;
        second.Transaction.State.Should().Be(TransactionState.RolledBack);
        pinsReleased.Should().Be(1);
        manager.DeadlockCount.Should().Be(1);
        manager.LastDeadlockVictim.Should().Be(second.Transaction.Id);
    }

    [Test]
    public async Task ThreeTransactionCycle_RollsBackHighestIdAndOtherTransactionsCanFinish()
    {
        var manager = new LockManager();
        var transactions = new TransactionManager();
        var first = new LockingTransaction(transactions.Begin(), manager);
        var second = new LockingTransaction(transactions.Begin(), manager);
        var third = new LockingTransaction(transactions.Begin(), manager);
        await first.AcquireAsync(First, LockMode.Exclusive);
        await second.AcquireAsync(Second, LockMode.Exclusive);
        await third.AcquireAsync(Third, LockMode.Exclusive);
        var firstWait = first.AcquireAsync(Second, LockMode.Exclusive).AsTask();
        var secondWait = second.AcquireAsync(Third, LockMode.Exclusive).AsTask();

        var victimWait = third.AcquireAsync(First, LockMode.Exclusive).AsTask();

        await ((Func<Task>)(async () => await victimWait)).Should().ThrowAsync<DeadlockException>();
        await secondWait;
        second.Commit();
        await firstWait;
        manager.LastDeadlockVictim.Should().Be(third.Transaction.Id);
        third.Transaction.State.Should().Be(TransactionState.RolledBack);
    }

    [Test]
    public async Task NonCyclicWait_IsNotReportedAsDeadlock()
    {
        var manager = new LockManager();
        await manager.AcquireAsync(new TransactionId(1), First, LockMode.Exclusive);
        var waiting = manager.AcquireAsync(new TransactionId(2), First, LockMode.Shared).AsTask();

        waiting.IsCompleted.Should().BeFalse();
        manager.DeadlockCount.Should().Be(0);
        manager.LastDeadlockVictim.Should().BeNull();
        manager.Release(new TransactionId(1), First);
        await waiting;
    }
}
