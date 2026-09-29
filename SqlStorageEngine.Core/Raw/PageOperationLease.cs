namespace SqlStorageEngine.Core;

internal sealed class PageOperationLease(SemaphoreSlim semaphore) : IDisposable
{
    private SemaphoreSlim? owned = semaphore;
    public void Dispose() => Interlocked.Exchange(ref owned, null)?.Release();
}
