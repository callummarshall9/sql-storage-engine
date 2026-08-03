using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class FaultInjectingPageStore(IPageStore inner, IPageAllocator allocator) : IPageStore, IPageAllocator
{
    public enum Operation { None, Read, Write, Flush, Allocate, Free }
    public Operation FailOn { get; set; }
    public int PageSize => inner.PageSize;
    public ValueTask ReadAsync(PageId id, Memory<byte> destination, CancellationToken token = default) =>
        FailOn == Operation.Read ? ValueTask.FromException(new IOException("Injected read failure.")) : inner.ReadAsync(id, destination, token);
    public ValueTask WriteAsync(PageId id, ReadOnlyMemory<byte> source, CancellationToken token = default) =>
        FailOn == Operation.Write ? ValueTask.FromException(new IOException("Injected write failure.")) : inner.WriteAsync(id, source, token);
    public ValueTask FlushAsync(CancellationToken token = default) =>
        FailOn == Operation.Flush ? ValueTask.FromException(new IOException("Injected flush failure.")) : inner.FlushAsync(token);
    public ValueTask<PageId> AllocateAsync(PageType type, CancellationToken token = default) =>
        FailOn == Operation.Allocate ? ValueTask.FromException<PageId>(new IOException("Injected allocation failure.")) : allocator.AllocateAsync(type, token);
    public ValueTask FreeAsync(PageId id, CancellationToken token = default) =>
        FailOn == Operation.Free ? ValueTask.FromException(new IOException("Injected free failure.")) : allocator.FreeAsync(id, token);
    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
