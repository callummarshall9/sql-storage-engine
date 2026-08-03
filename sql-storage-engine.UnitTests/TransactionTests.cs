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
        var manager = new TransactionManager();
        using var committed = manager.Begin(() => commits++, () => rollbacks++);
        using var rolledBack = manager.Begin(() => commits++, () => rollbacks++);
        committed.Commit();
        rolledBack.Rollback();
        committed.State.Should().Be(TransactionState.Committed);
        rolledBack.State.Should().Be(TransactionState.RolledBack);
        commits.Should().Be(1);
        rollbacks.Should().Be(1);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TerminalOperation_CannotExecuteTwice(bool commit)
    {
        using var transaction = new TransactionManager().Begin();
        if (commit) transaction.Commit(); else transaction.Rollback();
        ((Action)(() => { if (commit) transaction.Commit(); else transaction.Rollback(); })).Should()
            .Throw<InvalidOperationException>();
    }

    [Test]
    public void StorageOperation_RejectsEveryInactiveState()
    {
        using var committed = new TransactionManager().Begin();
        committed.Commit();
        ((Action)committed.EnsureActive).Should().Throw<InvalidOperationException>();
        using var rolledBack = new TransactionManager().Begin();
        rolledBack.Rollback();
        ((Action)rolledBack.EnsureActive).Should().Throw<InvalidOperationException>();
        using var failed = new TransactionManager().Begin(commit: () => throw new IOException("failure"));
        ((Action)failed.Commit).Should().Throw<IOException>();
        failed.State.Should().Be(TransactionState.Failed);
        ((Action)failed.EnsureActive).Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void DisposeActiveTransaction_InvokesRollbackExactlyOnce()
    {
        var rollbacks = 0;
        var transaction = new TransactionManager().Begin(rollback: () => rollbacks++);
        transaction.Dispose();
        transaction.Dispose();
        transaction.State.Should().Be(TransactionState.RolledBack);
        rollbacks.Should().Be(1);
    }

    [Test]
    public void ConcurrentBegin_AllocatesUniqueMonotonicIdsWithinIncarnation()
    {
        var manager = new TransactionManager();
        var transactions = Enumerable.Range(0, 1000).AsParallel().Select(_ => manager.Begin()).ToArray();
        try
        {
            transactions.Select(transaction => transaction.Id.Value).Distinct().Should().HaveCount(1000);
            transactions.Select(transaction => transaction.Id.Value).Order().Should().Equal(Enumerable.Range(1, 1000).Select(value => (ulong)value));
        }
        finally { foreach (var transaction in transactions) transaction.Dispose(); }
    }
}
