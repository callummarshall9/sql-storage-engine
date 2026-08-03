using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class BufferPoolTests
{
    [Test]
    public async Task RepeatedPageAccess_ReadsStoreOnceAndReturnsAPinToEveryCaller()
    {
        await using var store = new InMemoryPageStore();
        var pageId = await store.AllocateAsync(PageType.Heap);
        var counting = new CountingPageStore(store);
        await using var pool = new BufferPool(counting, 2, leaveOpen: true);

        using var first = await pool.GetPageAsync(pageId);
        using var second = await pool.GetPageAsync(pageId);

        counting.Reads.Should().Be(1);
        pool.FrameCount.Should().Be(1);
        pool.MissCount.Should().Be(1);
        pool.HitCount.Should().Be(1);
        first.PageId.Should().Be(pageId);
        second.PageId.Should().Be(pageId);
    }

    [Test]
    public async Task ConcurrentAccessToSamePage_CoalescesToOneFrameAndOneRead()
    {
        await using var store = new InMemoryPageStore();
        var pageId = await store.AllocateAsync(PageType.Heap);
        var counting = new CountingPageStore(store);
        await using var pool = new BufferPool(counting, 2, leaveOpen: true);

        var requests = Enumerable.Range(0, 12).Select(async _ => await pool.GetPageAsync(pageId)).ToArray();
        var pins = await Task.WhenAll(requests);
        try
        {
            counting.Reads.Should().Be(1);
            pool.FrameCount.Should().Be(1);
            pool.MissCount.Should().Be(1);
            pool.HitCount.Should().Be(11);
        }
        finally { foreach (var pin in pins) pin.Dispose(); }
    }

    [Test]
    public async Task FailedLoad_DoesNotPoisonCacheAndCanBeRetried()
    {
        await using var store = new InMemoryPageStore();
        var pageId = await store.AllocateAsync(PageType.Heap);
        await using var faulting = new FaultInjectingPageStore(store, store) { FailOn = FaultInjectingPageStore.Operation.Read };
        await using var pool = new BufferPool(faulting, 2, leaveOpen: true);

        await ((Func<Task>)(async () => await pool.GetPageAsync(pageId))).Should().ThrowAsync<IOException>();
        pool.FrameCount.Should().Be(0);
        faulting.FailOn = FaultInjectingPageStore.Operation.None;
        using var pin = await pool.GetPageAsync(pageId);

        pool.FrameCount.Should().Be(1);
        pool.MissCount.Should().Be(2);
        pool.HitCount.Should().Be(0);
    }

    [Test]
    public async Task ClockEviction_NeverExceedsCapacityAndSkipsPinnedFrames()
    {
        await using var store = new InMemoryPageStore();
        var pinnedId = await store.AllocateAsync(PageType.Heap);
        var evictableId = await store.AllocateAsync(PageType.Heap);
        var replacementId = await store.AllocateAsync(PageType.Heap);
        var counting = new CountingPageStore(store);
        await using var pool = new BufferPool(counting, 2, leaveOpen: true);
        using var pinned = await pool.GetPageAsync(pinnedId);
        (await pool.GetPageAsync(evictableId)).Dispose();

        (await pool.GetPageAsync(replacementId)).Dispose();

        pool.FrameCount.Should().Be(2);
        using var pinnedHit = await pool.GetPageAsync(pinnedId);
        counting.Reads.Should().Be(3);
        using var evictedReload = await pool.GetPageAsync(evictableId);
        counting.Reads.Should().Be(4);
        pool.FrameCount.Should().Be(2);
    }

    [Test]
    public async Task ClockEviction_AllFramesPinnedFailsPromptlyWithoutChangingCache()
    {
        await using var store = new InMemoryPageStore();
        var firstId = await store.AllocateAsync(PageType.Heap);
        var secondId = await store.AllocateAsync(PageType.Heap);
        await using var pool = new BufferPool(store, 1, leaveOpen: true);
        using var first = await pool.GetPageAsync(firstId);

        await ((Func<Task>)(async () => await pool.GetPageAsync(secondId)))
            .Should().ThrowAsync<StorageResourceExhaustedException>();

        pool.FrameCount.Should().Be(1);
        pool.MissCount.Should().Be(2);
    }

    [Test]
    public async Task ClockEviction_UnpinnedCandidateIsEventuallyReusedWithFreshMetadata()
    {
        await using var store = new InMemoryPageStore();
        var ids = new[]
        {
            await store.AllocateAsync(PageType.Heap),
            await store.AllocateAsync(PageType.Catalog),
            await store.AllocateAsync(PageType.Overflow)
        };
        var counting = new CountingPageStore(store);
        await using var pool = new BufferPool(counting, 1, leaveOpen: true);

        foreach (var id in ids) (await pool.GetPageAsync(id)).Dispose();

        pool.FrameCount.Should().Be(1);
        counting.Reads.Should().Be(3);
        using var lastHit = await pool.GetPageAsync(ids[^1]);
        counting.Reads.Should().Be(3);
        pool.HitCount.Should().Be(1);
    }

    [Test]
    public async Task Pool_CancellationAndDisposalOwnershipAreDeterministic()
    {
        await using var store = new InMemoryPageStore();
        var pageId = await store.AllocateAsync(PageType.Heap);
        var pool = new BufferPool(store, 1, leaveOpen: true);
        var cancelled = new CancellationToken(canceled: true);

        await ((Func<Task>)(async () => await pool.GetPageAsync(pageId, cancelled)))
            .Should().ThrowAsync<OperationCanceledException>();
        var pin = await pool.GetPageAsync(pageId);
        await ((Func<Task>)(async () => await pool.DisposeAsync()))
            .Should().ThrowAsync<StorageResourceException>();
        pin.Dispose();
        await pool.DisposeAsync();
        await pool.DisposeAsync();
        await ((Func<Task>)(async () => await pool.GetPageAsync(pageId)))
            .Should().ThrowAsync<ObjectDisposedException>();

        await store.FlushAsync();
    }
}
