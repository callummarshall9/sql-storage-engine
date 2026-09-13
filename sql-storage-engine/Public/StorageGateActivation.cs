namespace sql_storage_engine;

internal sealed class StorageGateActivation(StorageGate gate, StorageGateScope scope, StorageGateScope? previous) : IDisposable
{
    internal Task DrainAsync() => scope.End();

    public void Dispose()
    {
        // Callback owners explicitly await DrainAsync before resolving their journal.
        _ = scope.End();
        gate.Restore(previous);
    }
}
