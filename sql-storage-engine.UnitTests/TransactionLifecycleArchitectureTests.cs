using AwesomeAssertions;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class TransactionLifecycleArchitectureTests
{
    [Test]
    public void ExhaustedAllocationRemainsSaturatedAcrossRepeatedAttempts()
    {
        var manager = new TransactionManagerExposure((ulong)long.MaxValue - 1);
        using var last = new TransactionExposure(manager.CreateTransactionLifecycle());
        last.ReadId().Value.Should().Be((ulong)long.MaxValue);
        for (var attempt = 0; attempt < 4; attempt++)
            ((Action)(() => manager.CreateTransactionLifecycle())).Should().Throw<InvalidOperationException>();
        ((Action)(() => new TransactionManagerExposure(ulong.MaxValue))).Should().Throw<ArgumentOutOfRangeException>().WithParameterName("lastAllocatedId");
    }

    [Test]
    public void AliasedHandlesShareOneLifetimeAndForgedHandlesAreRejected()
    {
        var rollbacks = 0;
        var lifecycle = new TransactionManagerExposure().CreateTransactionLifecycle(rollback: () => Interlocked.Increment(ref rollbacks));
        var first = new TransactionExposure(lifecycle);
        var second = new TransactionExposure(lifecycle with { });
        Parallel.For(0, 64, index => { if ((index & 1) == 0) first.Dispose(); else second.Dispose(); });
        rollbacks.Should().Be(1);
        first.ReadState().Should().Be(TransactionState.RolledBack);
        second.ReadState().Should().Be(TransactionState.RolledBack);
        ((Action)(() => new TransactionExposure(lifecycle with { TransactionId = new TransactionId { Value = 99 } }))).Should().Throw<ArgumentException>();
        ((Action)(() => new TransactionExposure(new TransactionLifecycle { TransactionId = new TransactionId { Value = 1 } }))).Should().Throw<ArgumentException>();
    }

    [Test]
    public void FailedDisposalRetainsCallbackCauseAndDoesNotRetryRollback()
    {
        var calls = 0;
        var cause = new IOException("rollback failure");
        var transaction = new TransactionExposure(new TransactionManagerExposure().CreateTransactionLifecycle(rollback: () => { calls++; throw cause; }));
        ((Action)transaction.Dispose).Should().Throw<IOException>().Which.InnerException.Should().BeSameAs(cause);
        transaction.ReadState().Should().Be(TransactionState.Failed);
        transaction.Dispose();
        calls.Should().Be(1);
        ((Action)transaction.EnsureActive).Should().Throw<InvalidOperationException>();
    }
}