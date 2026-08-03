using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class BufferPoolFlushTests
{
    [Test]
    public async Task CleanEviction_PerformsNoWrite()
    {
        await using var store = new InMemoryPageStore();
        var first = await store.AllocateAsync(PageType.Heap);
        var second = await store.AllocateAsync(PageType.Heap);
        var counting = new CountingPageStore(store);
        await using var pool = new BufferPool(counting, 1, leaveOpen: true);
        (await pool.GetPageAsync(first)).Dispose();

        (await pool.GetPageAsync(second)).Dispose();

        counting.Writes.Should().Be(0);
    }

    [Test]
    public async Task DirtyEviction_WritesExactlyOneCompletePage()
    {
        await using var store = new InMemoryPageStore();
        var first = await store.AllocateAsync(PageType.Heap);
        var second = await store.AllocateAsync(PageType.Heap);
        var counting = new CountingPageStore(store);
        await using var pool = new BufferPool(counting, 1, leaveOpen: true);
        using (var pin = await pool.GetPageAsync(first))
        {
            pin.Memory.Span[^1] = 123;
            pin.MarkDirty(new LogSequenceNumber(8));
        }

        (await pool.GetPageAsync(second)).Dispose();

        counting.Writes.Should().Be(1);
        var persisted = new byte[store.PageSize];
        await store.ReadAsync(first, persisted);
        persisted[^1].Should().Be(123);
    }

    [Test]
    public async Task FailedDirtyEviction_RetainsOriginalPageAndDirtyState()
    {
        await using var store = new InMemoryPageStore();
        var first = await store.AllocateAsync(PageType.Heap);
        var second = await store.AllocateAsync(PageType.Heap);
        await using var faulting = new FaultInjectingPageStore(store, store);
        await using var pool = new BufferPool(faulting, 1, leaveOpen: true);
        using (var pin = await pool.GetPageAsync(first))
        {
            pin.Memory.Span[^1] = 44;
            pin.MarkDirty(new LogSequenceNumber(9));
        }
        faulting.FailOn = FaultInjectingPageStore.Operation.Write;

        await ((Func<Task>)(async () => await pool.GetPageAsync(second))).Should().ThrowAsync<IOException>();

        faulting.FailOn = FaultInjectingPageStore.Operation.None;
        using var retained = await pool.GetPageAsync(first);
        retained.IsDirty.Should().BeTrue();
        retained.PageLogSequenceNumber.Should().Be(new LogSequenceNumber(9));
        retained.Memory.Span[^1].Should().Be(44);
        pool.FrameCount.Should().Be(1);
    }

    [Test]
    public async Task ExplicitFlush_PersistsPinnedDirtyPageAndInvokesWalGuard()
    {
        await using var store = new InMemoryPageStore();
        var pageId = await store.AllocateAsync(PageType.Heap);
        var counting = new CountingPageStore(store);
        var guard = new RecordingFlushGuard();
        await using var pool = new BufferPool(counting, 1, leaveOpen: true, flushGuard: guard);
        using var pin = await pool.GetPageAsync(pageId);
        pin.Memory.Span[^1] = 55;
        pin.MarkDirty(new LogSequenceNumber(101));

        await pool.FlushPageAsync(pageId);

        pin.IsDirty.Should().BeFalse();
        counting.Writes.Should().Be(1);
        counting.Flushes.Should().Be(1);
        guard.Calls.Should().ContainSingle().Which.Should().Be((pageId, new LogSequenceNumber(101)));
        var persisted = new byte[store.PageSize];
        await store.ReadAsync(pageId, persisted);
        persisted[^1].Should().Be(55);
    }

    [Test]
    public async Task FlushAll_WritesEveryDirtyPageAndSkipsCleanPages()
    {
        await using var store = new InMemoryPageStore();
        var ids = new[]
        {
            await store.AllocateAsync(PageType.Heap),
            await store.AllocateAsync(PageType.Heap),
            await store.AllocateAsync(PageType.Heap)
        };
        var counting = new CountingPageStore(store);
        await using var pool = new BufferPool(counting, 3, leaveOpen: true);
        for (var index = 0; index < ids.Length; index++)
        {
            using var pin = await pool.GetPageAsync(ids[index]);
            if (index != 1) pin.MarkDirty(new LogSequenceNumber((ulong)index + 1));
        }

        await pool.FlushAllAsync();

        counting.Writes.Should().Be(2);
        counting.Flushes.Should().Be(1);
    }

    [Test]
    public async Task FailedExplicitFlush_LeavesPageDirtyForRetry()
    {
        await using var store = new InMemoryPageStore();
        var pageId = await store.AllocateAsync(PageType.Heap);
        await using var faulting = new FaultInjectingPageStore(store, store);
        await using var pool = new BufferPool(faulting, 1, leaveOpen: true);
        using var pin = await pool.GetPageAsync(pageId);
        pin.MarkDirty(new LogSequenceNumber(2));
        faulting.FailOn = FaultInjectingPageStore.Operation.Write;

        await ((Func<Task>)(async () => await pool.FlushPageAsync(pageId))).Should().ThrowAsync<IOException>();

        pin.IsDirty.Should().BeTrue();
        faulting.FailOn = FaultInjectingPageStore.Operation.None;
    }

    private sealed class RecordingFlushGuard : IPageFlushGuard
    {
        internal List<(PageId, LogSequenceNumber)> Calls { get; } = [];
        public ValueTask EnsureCanFlushAsync(PageId pageId, LogSequenceNumber pageLogSequenceNumber,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((pageId, pageLogSequenceNumber));
            return ValueTask.CompletedTask;
        }
    }
}
