using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Transactions;

/// <summary>Allocates transaction IDs monotonically within one database incarnation.</summary>
public sealed class TransactionManager
{
    private long _lastId;

    public TransactionManager(ulong lastAllocatedId = 0)
    {
        if (lastAllocatedId > long.MaxValue) throw new ArgumentOutOfRangeException(nameof(lastAllocatedId));
        _lastId = checked((long)lastAllocatedId);
    }

    /// <summary>Begins an active transaction with a unique nonzero ID.</summary>
    public Transaction Begin(Action? commit = null, Action? rollback = null)
    {
        var value = Interlocked.Increment(ref _lastId);
        if (value <= 0) throw new InvalidOperationException("Transaction ID space is exhausted.");
        return new Transaction(new TransactionId(checked((ulong)value)), commit, rollback);
    }
}
