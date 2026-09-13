namespace sql_storage_engine;

internal sealed class StorageScopedEnumerator<T>(IAsyncEnumerator<T> inner, StorageGate gate) : IAsyncEnumerator<T>
{
    private readonly SemaphoreSlim _operation = new(1, 1);
    private bool _disposed;
    public T Current => inner.Current;

    public async ValueTask<bool> MoveNextAsync()
    {
        if (!_operation.Wait(0)) throw new InvalidOperationException("A stream operation is already active.");
        try
        {
            if (_disposed) throw new InvalidOperationException("The statement scope has completed.");
            using var lease = await gate.EnterAsync(CancellationToken.None).ConfigureAwait(false);
            using var scope = lease.Activate();
            return await inner.MoveNextAsync().ConfigureAwait(false);
        }
        finally { _operation.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _operation.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            await inner.DisposeAsync().ConfigureAwait(false);
        }
        finally { _operation.Release(); }
    }
}
