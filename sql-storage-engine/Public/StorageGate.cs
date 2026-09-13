namespace sql_storage_engine;

// Reentrancy is limited to the active async call tree; concurrent sibling operations reject.
internal sealed class StorageGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly AsyncLocal<StorageGateScope?> _current = new();
    internal StorageGateScope? Current => _current.Value;

    internal async ValueTask<StorageGateLease> EnterAsync(CancellationToken token, bool reentrant = true)
    {
        token.ThrowIfCancellationRequested();
        var parent = reentrant ? Current : null;
        if (parent is not null)
        {
            parent.EnterChild();
            return new StorageGateLease(this, parent, false);
        }
        await _semaphore.WaitAsync(token).ConfigureAwait(false);
        return new StorageGateLease(this, null, true);
    }

    internal StorageGateLease EnterImmediate()
    {
        var parent = Current;
        if (parent is not null)
        {
            parent.EnterChild();
            return new StorageGateLease(this, parent, false);
        }
        if (!_semaphore.Wait(0)) throw new InvalidOperationException("Database access is busy; retry through an asynchronous transaction scope.");
        return new StorageGateLease(this, null, true);
    }

    internal StorageGateActivation Activate(StorageGateLease lease)
    {
        var previous = Current;
        var scope = new StorageGateScope();
        _current.Value = scope;
        return new StorageGateActivation(this, scope, previous);
    }

    internal void Restore(StorageGateScope? previous) => _current.Value = previous;
    internal void Release() => _semaphore.Release();
}
