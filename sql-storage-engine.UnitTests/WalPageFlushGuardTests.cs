using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class WalPageFlushGuardTests
{
    [Test]
    public async Task DirtyPageWritesOnlyAfterRequiredWalFlush()
    {
        var order = new List<string>();
        await using var store = new OrderedStore(order);
        var device = new OrderedWalDevice(order);
        var wal = await WriteAheadLog.OpenAsync(device);
        var record = await wal.AppendAsync(new TransactionId(1), WalRecordType.PageChange, default, ReadOnlyMemory<byte>.Empty);
        await using var pool = new BufferPool(store, 1, leaveOpen: true, new WalPageFlushGuard(wal));
        var id = await store.AllocateAsync(PageType.Heap);
        using (var pin = await pool.GetPageAsync(id)) pin.MarkDirty(record.Lsn);
        order.Clear();
        await pool.FlushPageAsync(id);
        order.Should().Equal("wal-flush", "page-write", "page-flush");
    }

    [Test]
    public async Task WalFlushFailurePreventsPageWriteAndPageRemainsDirtyForRetry()
    {
        var order = new List<string>();
        await using var store = new OrderedStore(order);
        var device = new OrderedWalDevice(order);
        var wal = await WriteAheadLog.OpenAsync(device);
        var record = await wal.AppendAsync(new TransactionId(1), WalRecordType.PageChange, default, ReadOnlyMemory<byte>.Empty);
        await using var pool = new BufferPool(store, 1, leaveOpen: true, new WalPageFlushGuard(wal));
        var id = await store.AllocateAsync(PageType.Heap);
        using (var pin = await pool.GetPageAsync(id)) pin.MarkDirty(record.Lsn);
        device.FailFlush = true;
        await ((Func<Task>)(async () => await pool.FlushPageAsync(id))).Should().ThrowAsync<IOException>();
        store.Writes.Should().Be(0);
        device.FailFlush = false;
        await pool.FlushPageAsync(id);
        store.Writes.Should().Be(1);
    }

    [Test]
    public async Task CleanPageRequiresNoWalFlush()
    {
        var order = new List<string>();
        await using var store = new OrderedStore(order);
        var device = new OrderedWalDevice(order);
        var wal = await WriteAheadLog.OpenAsync(device);
        await using var pool = new BufferPool(store, 1, leaveOpen: true, new WalPageFlushGuard(wal));
        var id = await store.AllocateAsync(PageType.Heap);
        using (await pool.GetPageAsync(id)) { }
        await pool.FlushPageAsync(id);
        order.Should().NotContain("wal-flush");
    }

    private sealed class OrderedWalDevice(List<string> order) : WriteAheadLogTests.MemoryWalDevice
    {
        public override ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            order.Add("wal-flush");
            return base.FlushAsync(cancellationToken);
        }
    }

    private sealed class OrderedStore(List<string> order) : IPageStore, IPageAllocator
    {
        private readonly InMemoryPageStore _inner = new();
        public int Writes { get; private set; }
        public int PageSize => _inner.PageSize;
        public ValueTask<PageId> AllocateAsync(PageType type, CancellationToken token = default) => _inner.AllocateAsync(type, token);
        public ValueTask FreeAsync(PageId id, CancellationToken token = default) => _inner.FreeAsync(id, token);
        public ValueTask ReadAsync(PageId id, Memory<byte> bytes, CancellationToken token = default) => _inner.ReadAsync(id, bytes, token);
        public async ValueTask WriteAsync(PageId id, ReadOnlyMemory<byte> bytes, CancellationToken token = default)
        { order.Add("page-write"); Writes++; await _inner.WriteAsync(id, bytes, token); }
        public async ValueTask FlushAsync(CancellationToken token = default)
        { order.Add("page-flush"); await _inner.FlushAsync(token); }
        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}
