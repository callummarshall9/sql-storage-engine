using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class TableHeapScanTests
{
    [Test]
    public async Task Scan_ReturnsEveryLiveRowOnceInPageAndSlotOrderAndOmitsDeletedRows()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 3, leaveOpen: true);
        var table = await TableHeap.CreateAsync(pool, store);
        var inserted = new List<(RowId Id, byte[] Bytes)>();
        for (var index = 0; index < 7; index++)
        {
            var bytes = Enumerable.Repeat((byte)index, 2500).ToArray();
            inserted.Add((await table.InsertAsync(bytes), bytes));
        }
        var deleted = inserted[2].Id;
        using (var pin = await pool.GetPageAsync(deleted.PageId))
        {
            new HeapPage(pin.Memory, deleted.PageId).Delete(deleted.SlotId, deleted.Generation).Should().BeTrue();
            pin.MarkDirty(new LogSequenceNumber(0));
        }

        var scanned = await CollectAsync(table.ScanAsync());

        scanned.Select(item => item.RowId).Should().Equal(inserted.Where(item => item.Id != deleted).Select(item => item.Id));
        scanned.Select(item => item.Row.ToArray()).Should().BeEquivalentTo(
            inserted.Where(item => item.Id != deleted).Select(item => item.Bytes), options => options.WithStrictOrdering());
    }

    [Test]
    public async Task Scan_EmptyHeapYieldsNoRowsAndEarlyBreakLeavesNoPins()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var table = await TableHeap.CreateAsync(pool, store);
        (await CollectAsync(table.ScanAsync())).Should().BeEmpty();
        await table.InsertAsync(new byte[] { 1 });
        await table.InsertAsync(new byte[] { 2 });

        await foreach (var _ in table.ScanAsync()) break;

        pool.PinnedPageCount.Should().Be(0);
    }

    [Test]
    public async Task Scan_CancellationReleasesPinsAndCyclesAreDetected()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var table = await TableHeap.CreateAsync(pool, store);
        await table.InsertAsync(new byte[] { 1 });
        var cancelled = new CancellationToken(canceled: true);
        await ((Func<Task>)(async () => await CollectAsync(table.ScanAsync(cancelled))))
            .Should().ThrowAsync<OperationCanceledException>();
        pool.PinnedPageCount.Should().Be(0);

        using (var pin = await pool.GetPageAsync(table.RootPageId))
        {
            pin.Memory.Span[HeapPageLayout.NextPageOffset] = 1;
            BinaryPrimitives.WriteUInt64LittleEndian(
                pin.Memory.Span[(HeapPageLayout.NextPageOffset + 1)..], table.RootPageId.Value);
            pin.MarkDirty(new LogSequenceNumber(0));
        }
        await ((Func<Task>)(async () => await CollectAsync(table.ScanAsync())))
            .Should().ThrowAsync<StorageCorruptionException>();
        pool.PinnedPageCount.Should().Be(0);
    }

    [Test]
    public async Task Scan_InvalidPageLinkIsReportedAsCorruption()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var table = await TableHeap.CreateAsync(pool, store);
        using (var pin = await pool.GetPageAsync(table.RootPageId))
        {
            pin.Memory.Span[HeapPageLayout.NextPageOffset] = 1;
            BinaryPrimitives.WriteUInt64LittleEndian(pin.Memory.Span[(HeapPageLayout.NextPageOffset + 1)..], 999);
            pin.MarkDirty(new LogSequenceNumber(0));
        }

        await ((Func<Task>)(async () => await CollectAsync(table.ScanAsync())))
            .Should().ThrowAsync<StorageCorruptionException>();
    }

    private static async Task<List<(RowId RowId, ReadOnlyMemory<byte> Row)>> CollectAsync(
        IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> rows)
    {
        var result = new List<(RowId, ReadOnlyMemory<byte>)>();
        await foreach (var row in rows) result.Add(row);
        return result;
    }
}
