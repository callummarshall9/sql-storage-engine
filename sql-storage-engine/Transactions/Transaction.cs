using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Transactions;

/// <summary>Describes the externally observable lifecycle of a transaction.</summary>
public enum TransactionState
{
    Active = 1,
    Committed = 2,
    RolledBack = 3,
    Failed = 4
}

/// <summary>Represents one atomic unit of storage work.</summary>
public interface ITransaction : IDisposable
{
    TransactionId Id { get; }
    TransactionState State { get; }
    void EnsureActive();
    void Commit();
    void Rollback();
}

/// <summary>Owns one validated transaction lifecycle and rolls active work back on disposal.</summary>
public sealed class Transaction : ITransaction
{
    private readonly Action _commit;
    private readonly Action _rollback;
    private readonly object _sync = new();

    internal Transaction(TransactionId id, Action? commit = null, Action? rollback = null)
    {
        if (id.Value == 0) throw new ArgumentOutOfRangeException(nameof(id));
        Id = id;
        _commit = commit ?? (() => { });
        _rollback = rollback ?? (() => { });
        State = TransactionState.Active;
    }

    public TransactionId Id { get; }
    public TransactionState State { get; private set; }

    /// <summary>Rejects a storage operation after this transaction has reached any terminal state.</summary>
    public void EnsureActive()
    {
        lock (_sync)
            if (State != TransactionState.Active)
                throw new InvalidOperationException($"Transaction {Id} is {State} and cannot perform storage operations.");
    }

    /// <summary>Commits exactly once; a callback failure moves the transaction to Failed.</summary>
    public void Commit()
    {
        lock (_sync)
        {
            EnsureActiveCore(nameof(Commit));
            try { _commit(); State = TransactionState.Committed; }
            catch { State = TransactionState.Failed; throw; }
        }
    }

    /// <summary>Rolls back exactly once; a callback failure moves the transaction to Failed.</summary>
    public void Rollback()
    {
        lock (_sync)
        {
            EnsureActiveCore(nameof(Rollback));
            try { _rollback(); State = TransactionState.RolledBack; }
            catch { State = TransactionState.Failed; throw; }
        }
    }

    /// <summary>Rolls back active work; disposing a terminal transaction has no further effect.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (State != TransactionState.Active) return;
            try { _rollback(); State = TransactionState.RolledBack; }
            catch { State = TransactionState.Failed; throw; }
        }
    }

    private void EnsureActiveCore(string operation)
    {
        if (State != TransactionState.Active)
            throw new InvalidOperationException($"Cannot execute {operation} when transaction {Id} is {State}.");
    }
}

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
