using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

internal sealed class CountingPageStore(IPageStore inner, bool leaveOpen = true) : IPageStore
{
    private int _reads;
    private int _writes;
    private int _flushes;
    public int Reads => Volatile.Read(ref _reads);
    public int Writes => Volatile.Read(ref _writes);
    public int Flushes => Volatile.Read(ref _flushes);
    public int PageSize => inner.PageSize;

    public async ValueTask ReadAsync(PageId id, Memory<byte> destination, CancellationToken token = default)
    {
        Interlocked.Increment(ref _reads);
        await inner.ReadAsync(id, destination, token);
    }

    public async ValueTask WriteAsync(PageId id, ReadOnlyMemory<byte> source, CancellationToken token = default)
    {
        Interlocked.Increment(ref _writes);
        await inner.WriteAsync(id, source, token);
    }

    public async ValueTask FlushAsync(CancellationToken token = default)
    {
        Interlocked.Increment(ref _flushes);
        await inner.FlushAsync(token);
    }

    public ValueTask DisposeAsync() => leaveOpen ? ValueTask.CompletedTask : inner.DisposeAsync();
}
