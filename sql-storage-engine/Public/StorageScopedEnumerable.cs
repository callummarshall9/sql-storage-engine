namespace sql_storage_engine;

internal sealed class StorageScopedEnumerable<T>(IAsyncEnumerable<T> source, StorageGateScope scope) : IAsyncEnumerable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var iterator = new StorageScopedEnumerator<T>(source.GetAsyncEnumerator(cancellationToken));
        scope.Register(iterator);
        return iterator;
    }
}
