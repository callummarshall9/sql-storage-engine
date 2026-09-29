using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Transactions;

/// <summary>Releases every lock owned by a transaction on commit, rollback, failure, or disposal.</summary>
public sealed class LockingTransaction : IDisposable
{
    private readonly ILockManager _lockManager;
    private readonly Action _releasePins;

    public LockingTransaction(Transaction transaction, ILockManager lockManager, Action? releasePins = null)
    {
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        _lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
        _releasePins = releasePins ?? (() => { });
        if (lockManager is LockManager concrete)
            concrete.RegisterVictimHandler(transaction.Id, AbortForDeadlock);
    }

    public Transaction Transaction { get; }
    public ValueTask AcquireAsync(LockResource resource, LockMode mode, CancellationToken cancellationToken = default)
    {
        Transaction.EnsureActive();
        return _lockManager.AcquireAsync(Transaction.Id, resource, mode, cancellationToken);
    }

    public ValueTask ConvertAsync(LockResource resource, LockMode mode, CancellationToken cancellationToken = default)
    {
        Transaction.EnsureActive();
        return _lockManager.ConvertAsync(Transaction.Id, resource, mode, cancellationToken);
    }

    /// <summary>Releases one owned lock early when the selected isolation level permits it.</summary>
    public bool Release(LockResource resource) => _lockManager.Release(Transaction.Id, resource);

    public void Commit()
    {
        try { Transaction.Commit(); }
        finally { _lockManager.ReleaseAll(Transaction.Id); }
    }

    public void Rollback()
    {
        try { Transaction.Rollback(); }
        finally { _lockManager.ReleaseAll(Transaction.Id); }
    }

    public void Dispose()
    {
        try { Transaction.Dispose(); }
        finally { _lockManager.ReleaseAll(Transaction.Id); }
    }

    private void AbortForDeadlock()
    {
        try
        {
            if (Transaction.State == TransactionState.Active) Transaction.Rollback();
        }
        finally { _releasePins(); }
    }
}
