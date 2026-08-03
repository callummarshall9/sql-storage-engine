using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class OverflowManagerTests
{
    [TestCase(100)]
    [TestCase(20000)]
    public async Task WriteAndRead_OneAndMultiplePagesRoundTrip(int length)
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 4, leaveOpen: true);
        var manager = new OverflowManager(pool, store);
        var expected = new byte[length];
        new Random(1234 + length).NextBytes(expected);

        var reference = await manager.WriteAsync(expected);
        var actual = await manager.ReadAsync(reference);

        actual.ToArray().Should().Equal(expected);
        pool.PinnedPageCount.Should().Be(0);
    }

    [Test]
    public async Task Read_RejectsTruncatedOverlongCyclicAndWrongTypeChains()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 4, leaveOpen: true);
        var manager = new OverflowManager(pool, store);
        var capacity = OverflowPageCodec.GetPayloadCapacity(store.PageSize);
        var reference = await manager.WriteAsync(new byte[capacity + 10]);

        await ((Func<Task>)(async () => await manager.ReadAsync(reference with { TotalLength = capacity + 20 })))
            .Should().ThrowAsync<StorageCorruptionException>();
        await ((Func<Task>)(async () => await manager.ReadAsync(reference with { TotalLength = capacity - 1 })))
            .Should().ThrowAsync<StorageCorruptionException>();

        using (var pin = await pool.GetPageAsync(reference.FirstPageId))
        {
            pin.Memory.Span[OverflowPageCodec.NextPageOffset] = 1;
            BinaryPrimitives.WriteUInt64LittleEndian(pin.Memory.Span[(OverflowPageCodec.NextPageOffset + 1)..], reference.FirstPageId.Value);
            PageChecksum.WriteChecksum(pin.Memory.Span, pool.PageSize);
            pin.MarkDirty(new LogSequenceNumber(0));
        }
        await ((Func<Task>)(async () => await manager.ReadAsync(reference)))
            .Should().ThrowAsync<StorageCorruptionException>();

        var heapId = await store.AllocateAsync(PageType.Heap);
        var heapBytes = new byte[store.PageSize];
        Heap.HeapPageLayout.Initialize(heapBytes, heapId);
        await store.WriteAsync(heapId, heapBytes);
        await ((Func<Task>)(async () => await manager.ReadAsync(new OverflowReference(heapId, 1))))
            .Should().ThrowAsync<StorageFormatException>();
    }

    [Test]
    public async Task FailedAllocation_FreesEveryPreviouslyAllocatedPage()
    {
        await using var store = new InMemoryPageStore();
        var allocator = new FailingAllocator(store, failOnCall: 2);
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var manager = new OverflowManager(pool, allocator);
        var capacity = OverflowPageCodec.GetPayloadCapacity(store.PageSize);

        await ((Func<Task>)(async () => await manager.WriteAsync(new byte[capacity + 1])))
            .Should().ThrowAsync<IOException>();

        allocator.Allocated.Should().ContainSingle();
        allocator.Freed.Should().Equal(allocator.Allocated);
        (await store.AllocateAsync(PageType.Overflow)).Should().Be(allocator.Allocated[0]);
        pool.PinnedPageCount.Should().Be(0);
    }

    [Test]
    public async Task OverflowValue_SurvivesFlushCloseAndReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sql-overflow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "database.sse");
        var expected = new byte[20000];
        new Random(2026).NextBytes(expected);
        try
        {
            OverflowReference reference;
            await using (var database = await PageDatabase.CreateAsync(path))
            {
                await using var pool = new BufferPool(database, 4, leaveOpen: true);
                reference = await new OverflowManager(pool, database).WriteAsync(expected);
                await pool.FlushAllAsync();
            }
            await using (var reopened = await PageDatabase.OpenAsync(path))
            await using (var pool = new BufferPool(reopened, 4, leaveOpen: true))
            {
                var actual = await new OverflowManager(pool, reopened).ReadAsync(reference);
                actual.ToArray().Should().Equal(expected);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class FailingAllocator(IPageAllocator inner, int failOnCall) : IPageAllocator
    {
        private int _calls;
        internal List<PageId> Allocated { get; } = [];
        internal List<PageId> Freed { get; } = [];
        public async ValueTask<PageId> AllocateAsync(PageType pageType, CancellationToken cancellationToken = default)
        {
            if (++_calls == failOnCall) throw new IOException("Injected allocation failure.");
            var id = await inner.AllocateAsync(pageType, cancellationToken);
            Allocated.Add(id);
            return id;
        }
        public async ValueTask FreeAsync(PageId pageId, CancellationToken cancellationToken = default)
        {
            Freed.Add(pageId);
            await inner.FreeAsync(pageId, cancellationToken);
        }
    }
}
