// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Exposures.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class OverflowManagerTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Free_ValidatesTheEntireChainBeforeRetiringAnyPage(bool corruptLastPage)
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var allocator = new FailingAllocator(store, int.MaxValue);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 4, leaveOpen: true);
        var manager = new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, allocator);
        var capacity = sql_storage_engine.Exposures.Overflow.OverflowPageHeaderExposure.CalculateOverflowPageHeaderCapacity(store.GetPageSize());
        var reference = await manager.WriteAsync(new byte[capacity + 10]);
        if (corruptLastPage)
        {
            using var pin = await pool.GetPageAsync(allocator.Allocated[^1]);
            pin.ReadMemory().Span[sql_storage_engine.Models.Overflow.OverflowPageFormat.HeaderLength] ^= 1;
            pin.MarkDirty(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 });
        }

        var invalid = corruptLastPage ? reference : reference with
        {
            TotalLength = capacity + 11
        };
        await ((Func<Task>)(async () => await manager.FreeAsync(invalid))).Should().ThrowAsync<StorageCorruptionException>();
        allocator.Freed.Should().BeEmpty();
        pool.ReadPinnedPageCount().Should().Be(0);
    }

    [Test]
    public async Task Free_ValidatedChainRetiresEveryOwnedPageInReverseOrder()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var allocator = new FailingAllocator(store, int.MaxValue);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 4, leaveOpen: true);
        var manager = new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, allocator);
        var reference = await manager.WriteAsync(new byte[20000]);
        await manager.FreeAsync(reference);
        allocator.Freed.Should().Equal(allocator.Allocated.AsEnumerable().Reverse());
        pool.ReadPinnedPageCount().Should().Be(0);
    }

    [TestCase(100)]
    [TestCase(20000)]
    public async Task WriteAndRead_OneAndMultiplePagesRoundTrip(int length)
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 4, leaveOpen: true);
        var manager = new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, store);
        var expected = new byte[length];
        new Random(1234 + length).NextBytes(expected);
        var reference = await manager.WriteAsync(expected);
        var actual = await manager.ReadAsync(reference);
        actual.ToArray().Should().Equal(expected);
        pool.ReadPinnedPageCount().Should().Be(0);
    }

    [Test]
    public async Task Read_RejectsTruncatedOverlongCyclicAndWrongTypeChains()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 4, leaveOpen: true);
        var manager = new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, store);
        var capacity = sql_storage_engine.Exposures.Overflow.OverflowPageHeaderExposure.CalculateOverflowPageHeaderCapacity(store.GetPageSize());
        var reference = await manager.WriteAsync(new byte[capacity + 10]);
        await ((Func<Task>)(async () => await manager.ReadAsync(reference with { TotalLength = capacity + 20 }))).Should().ThrowAsync<StorageCorruptionException>();
        await ((Func<Task>)(async () => await manager.ReadAsync(reference with { TotalLength = capacity - 1 }))).Should().ThrowAsync<StorageCorruptionException>();
        using (var pin = await pool.GetPageAsync(reference.FirstPageId))
        {
            pin.ReadMemory().Span[sql_storage_engine.Models.Overflow.OverflowPageFormat.NextPageOffset] = 1;
            BinaryPrimitives.WriteUInt64LittleEndian(pin.ReadMemory().Span[(sql_storage_engine.Models.Overflow.OverflowPageFormat.NextPageOffset + 1)..],reference.FirstPageId.Value);
            PageChecksumExposure.WriteChecksum(pin.ReadMemory().Span,pool.ReadPageSize());
            pin.MarkDirty(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 });
        }

        await ((Func<Task>)(async () => await manager.ReadAsync(reference))).Should().ThrowAsync<StorageCorruptionException>();
        var heapId = await store.AllocateAsync(PageType.Heap);
        var heapBytes = new byte[store.GetPageSize()];
        sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.InitializeHeapPageLayout(heapBytes,(heapId).Value);
        await store.WriteAsync(heapId,heapBytes);
        await ((Func<Task>)(async () => await manager.ReadAsync(new OverflowReference { FirstPageId = heapId, TotalLength = 1 }))).Should().ThrowAsync<StorageFormatException>();
    }

    [Test]
    public async Task FailedAllocation_FreesEveryPreviouslyAllocatedPage()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var allocator = new FailingAllocator(store, failOnCall: 2);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 2, leaveOpen: true);
        var manager = new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, allocator);
        var capacity = sql_storage_engine.Exposures.Overflow.OverflowPageHeaderExposure.CalculateOverflowPageHeaderCapacity(store.GetPageSize());
        await ((Func<Task>)(async () => await manager.WriteAsync(new byte[capacity + 1]))).Should().ThrowAsync<IOException>();
        allocator.Allocated.Should().ContainSingle();
        allocator.Freed.Should().Equal(allocator.Allocated);
        (await store.AllocateAsync(PageType.Overflow)).Should().Be(allocator.Allocated[0]);
        pool.ReadPinnedPageCount().Should().Be(0);
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
            await using (var database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 4, leaveOpen: true);
                reference = await new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, database).WriteAsync(expected);
                await pool.FlushAllAsync();
            }

            await using (var reopened = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path))
            await using (sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(reopened, 4, leaveOpen: true))
            {
                var actual = await new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, reopened).ReadAsync(reference);
                actual.ToArray().Should().Equal(expected);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class FailingAllocator(sql_storage_engine.Exposures.Pages.IPageAllocatorExposure inner, int failOnCall) : sql_storage_engine.Exposures.Pages.IPageAllocatorExposure
    {
        private int _calls;
        internal List<PageId> Allocated { get; } = [];
        internal List<PageId> Freed { get; } = [];

        public async ValueTask<PageId> AllocateAsync(PageType pageType, CancellationToken cancellationToken = default)
        {
            if (++_calls == failOnCall)
                throw new IOException("Injected allocation failure.");
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