using AwesomeAssertions;
using sql_storage_engine.Exposures.Buffers;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class BufferPoolArchitectureTests
{
    [Test]
    public async Task AsynchronousPinDisposalReleasesOnlyItsViewAndKeepsThePoolUsable()
    {
        await using var store = new MemoryPageStoreExposure();
        var pageId = await store.AllocateAsync(PageType.Heap);
        await using var pool = new BufferPoolExposure(store, 2, leaveOpen: true);
        var first = await pool.GetPageAsync(pageId);
        using var second = await pool.GetPageAsync(pageId);
        pool.ReadPinnedPageCount().Should().Be(1);
        await ((IAsyncDisposable)first).DisposeAsync();
        ((Action)(() => first.ReadMemory())).Should().Throw<ObjectDisposedException>();
        second.ReadMemory().Length.Should().Be(store.GetPageSize());
        using var third = await pool.GetPageAsync(pageId);
        pool.ReadHitCount().Should().Be(2);
        pool.ReadPinnedPageCount().Should().Be(1);
        first.Dispose();
        third.ReadPageId().Should().Be(pageId);
    }
    [Test]
    public async Task ResourceLeaseMarksItsPageAndRejectsDirtyCallbacksAfterRelease()
    {
        await using var store = new MemoryPageStoreExposure();
        var pageId = await store.AllocateAsync(PageType.Heap);
        await using var pool = new BufferPoolExposure(store, 1, leaveOpen: true);
        var lease = await pool.GetPageLeaseAsync(pageId);
        lease.Memory.Span[^1] = 42;
        lease.MarkDirty(0);
        pool.ReadPinnedPageCount().Should().Be(1);
        lease.Lease.Dispose();
        pool.ReadPinnedPageCount().Should().Be(0);
        ((Action)(() => lease.MarkDirty(0))).Should().Throw<ObjectDisposedException>();
        await pool.FlushAllAsync();
        var bytes = new byte[store.GetPageSize()];
        await store.ReadAsync(pageId,bytes);
        bytes[^1].Should().Be(42);
    }
}