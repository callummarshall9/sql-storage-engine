namespace sql_storage_engine;

internal sealed class StorageScopedEnumerable<T>(IAsyncEnumerable<T> source, StorageGateScope scope, StorageGate gate) : IAsyncEnumerable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var iterator = new StorageScopedEnumerator<T>(source.GetAsyncEnumerator(cancellationToken), gate);
        scope.Register(iterator);
        return iterator;
    }
}
