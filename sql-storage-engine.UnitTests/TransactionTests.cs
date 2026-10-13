using AwesomeAssertions;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;
public sealed class TransactionTests
{
    [Test]
    public void CommitAndRollback_ApplyValidTerminalTransitions()
    {
        var commits = 0;
        var rollbacks = 0;
        sql_storage_engine.Exposures.Transactions.TransactionManagerExposure manager = new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure();
        using sql_storage_engine.Exposures.Transactions.TransactionExposure committed = new sql_storage_engine.Exposures.Transactions.TransactionExposure(manager.CreateTransactionLifecycle(() => commits++,() => rollbacks++));
        using sql_storage_engine.Exposures.Transactions.TransactionExposure rolledBack = new sql_storage_engine.Exposures.Transactions.TransactionExposure(manager.CreateTransactionLifecycle(() => commits++,() => rollbacks++));
        committed.Commit();
        rolledBack.Rollback();
        committed.ReadState().Should().Be(TransactionState.Committed);
        rolledBack.ReadState().Should().Be(TransactionState.RolledBack);
        commits.Should().Be(1);
        rollbacks.Should().Be(1);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TerminalOperation_CannotExecuteTwice(bool commit)
    {
        using sql_storage_engine.Exposures.Transactions.TransactionExposure transaction = new sql_storage_engine.Exposures.Transactions.TransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure().CreateTransactionLifecycle());
        if (commit)
            transaction.Commit();
        else
            transaction.Rollback();
        ((Action)(() =>
        {
            if (commit)
                transaction.Commit();
            else
                transaction.Rollback();
        })).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void StorageOperation_RejectsEveryInactiveState()
    {
        using sql_storage_engine.Exposures.Transactions.TransactionExposure committed = new sql_storage_engine.Exposures.Transactions.TransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure().CreateTransactionLifecycle());
        committed.Commit();
        ((Action)committed.EnsureActive).Should().Throw<InvalidOperationException>();
        using sql_storage_engine.Exposures.Transactions.TransactionExposure rolledBack = new sql_storage_engine.Exposures.Transactions.TransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure().CreateTransactionLifecycle());
        rolledBack.Rollback();
        ((Action)rolledBack.EnsureActive).Should().Throw<InvalidOperationException>();
        using sql_storage_engine.Exposures.Transactions.TransactionExposure failed = new sql_storage_engine.Exposures.Transactions.TransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure().CreateTransactionLifecycle(commit: () => throw new IOException("failure")));
        ((Action)failed.Commit).Should().Throw<IOException>();
        failed.ReadState().Should().Be(TransactionState.Failed);
        ((Action)failed.EnsureActive).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void DisposeActiveTransaction_InvokesRollbackExactlyOnce()
    {
        var rollbacks = 0;
        sql_storage_engine.Exposures.Transactions.TransactionExposure transaction = new sql_storage_engine.Exposures.Transactions.TransactionExposure(new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure().CreateTransactionLifecycle(rollback: () => rollbacks++));
        transaction.Dispose();
        transaction.Dispose();
        transaction.ReadState().Should().Be(TransactionState.RolledBack);
        rollbacks.Should().Be(1);
    }

    [Test]
    public void ConcurrentBegin_AllocatesUniqueMonotonicIdsWithinIncarnation()
    {
        sql_storage_engine.Exposures.Transactions.TransactionManagerExposure manager = new sql_storage_engine.Exposures.Transactions.TransactionManagerExposure();
        var transactions = Enumerable.Range(0, 1000).AsParallel().Select(_ => new sql_storage_engine.Exposures.Transactions.TransactionExposure(manager.CreateTransactionLifecycle())).ToArray();
        try
        {
            transactions.Select(transaction => transaction.ReadId().Value).Distinct().Should().HaveCount(1000);
            transactions.Select(transaction => transaction.ReadId().Value).Order().Should().Equal(Enumerable.Range(1, 1000).Select(value => (ulong)value));
        }
        finally
        {
            foreach (sql_storage_engine.Exposures.Transactions.TransactionExposure transaction in transactions)
                transaction.Dispose();
        }
    }
}