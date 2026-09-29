using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Transactions;

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
