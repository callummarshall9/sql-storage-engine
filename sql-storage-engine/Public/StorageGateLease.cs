namespace sql_storage_engine;

internal sealed class StorageGateLease(StorageGate gate, StorageGateScope? parent, bool ownsSemaphore) : IDisposable
{
    private int _disposed;
    internal StorageGateActivation Activate()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return gate.Activate(this);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        if (ownsSemaphore) gate.Release();
        else parent!.ExitChild();
    }
}
