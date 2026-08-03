namespace sql_storage_engine.Transactions;

public enum TransactionAccess { Read, Write }

/// <summary>Provides cancellation-safe many-reader/single-writer database coordination.</summary>
public sealed class TransactionCoordinator
{
    private readonly SemaphoreSlim _turnstile = new(1, 1);
    private readonly SemaphoreSlim _resource = new(1, 1);
    private readonly SemaphoreSlim _readerMutex = new(1, 1);
    private int _readers;

    public async ValueTask<IDisposable> AcquireAsync(TransactionAccess access,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(access)) throw new ArgumentOutOfRangeException(nameof(access));
        if (access == TransactionAccess.Write)
        {
            await _turnstile.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await _resource.WaitAsync(cancellationToken).ConfigureAwait(false); }
            catch { _turnstile.Release(); throw; }
            return new Lease(() => { _resource.Release(); _turnstile.Release(); });
        }

        await _turnstile.WaitAsync(cancellationToken).ConfigureAwait(false);
        _turnstile.Release();
        await _readerMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_readers == 0) await _resource.WaitAsync(cancellationToken).ConfigureAwait(false);
            _readers++;
        }
        finally { _readerMutex.Release(); }
        return new Lease(ReleaseReader);
    }

    private void ReleaseReader()
    {
        _readerMutex.Wait();
        try { if (--_readers == 0) _resource.Release(); }
        finally { _readerMutex.Release(); }
    }

    private sealed class Lease(Action release) : IDisposable
    {
        private Action? _release = release;
        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}

/// <summary>Binds a transaction lifecycle to a database coordination lease.</summary>
public sealed class CoordinatedTransaction : IDisposable
{
    private readonly IDisposable _lease;
    public CoordinatedTransaction(Transaction transaction, IDisposable lease)
    { Transaction = transaction; _lease = lease; }
    public Transaction Transaction { get; }
    public void Commit() { try { Transaction.Commit(); } finally { _lease.Dispose(); } }
    public void Rollback() { try { Transaction.Rollback(); } finally { _lease.Dispose(); } }
    public void Dispose() { try { Transaction.Dispose(); } finally { _lease.Dispose(); } }
}
