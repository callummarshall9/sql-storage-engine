using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class TableHeapTests
{
    [Test]
    public async Task InsertAcrossPages_ReturnsRetrievableCompleteRowIdsAndPreservesChain()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 3, leaveOpen: true);
        var table = await TableHeap.CreateAsync(pool, store);
        var rows = Enumerable.Range(0, 5).Select(index => Enumerable.Repeat((byte)index, 3000).ToArray()).ToArray();
        var ids = new List<RowId>();
        foreach (var row in rows) ids.Add(await table.InsertAsync(row));

        ids.Select(id => id.PageId).Distinct().Count().Should().BeGreaterThan(1);
        for (var index = 0; index < rows.Length; index++)
        {
            var lookup = await table.ReadAsync(ids[index]);
            lookup.Result.Should().Be(TableHeapLookupResult.Found);
            lookup.Row.ToArray().Should().Equal(rows[index]);
        }
        pool.PinnedPageCount.Should().Be(0);
    }

    [Test]
    public async Task Lookup_DistinguishesUnknownPageSlotDeletedAndStaleGeneration()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var table = await TableHeap.CreateAsync(pool, store);
        var id = await table.InsertAsync(new byte[] { 1 });

        (await table.ReadAsync(id with { PageId = new PageId(999) })).Result.Should().Be(TableHeapLookupResult.UnknownPage);
        (await table.ReadAsync(id with { SlotId = new SlotId(999) })).Result.Should().Be(TableHeapLookupResult.UnknownSlot);
        (await table.ReadAsync(id with { Generation = new SlotGeneration(id.Generation.Value + 1) })).Result
            .Should().Be(TableHeapLookupResult.StaleGeneration);
        using (var pin = await pool.GetPageAsync(id.PageId))
        {
            new HeapPage(pin.Memory, id.PageId).Delete(id.SlotId, id.Generation).Should().BeTrue();
            pin.MarkDirty(new LogSequenceNumber(0));
        }
        (await table.ReadAsync(id)).Result.Should().Be(TableHeapLookupResult.Deleted);
    }

    [Test]
    public async Task AllocationFailure_ReleasesEveryPagePin()
    {
        await using var store = new InMemoryPageStore();
        await using var faulting = new FaultInjectingPageStore(store, store);
        await using var pool = new BufferPool(faulting, 2, leaveOpen: true);
        var table = await TableHeap.CreateAsync(pool, faulting);
        await table.InsertAsync(new byte[store.PageSize - HeapPageLayout.HeaderLength - HeapPageLayout.SlotEntryLength]);
        faulting.FailOn = FaultInjectingPageStore.Operation.Allocate;

        await ((Func<Task>)(async () => await table.InsertAsync(new byte[] { 2 }))).Should().ThrowAsync<IOException>();

        pool.PinnedPageCount.Should().Be(0);
        faulting.FailOn = FaultInjectingPageStore.Operation.None;
    }

    [Test]
    public async Task RowsSurviveFlushCloseAndReopen()
    {
        var (directory, path) = NewPath();
        try
        {
            RowId id;
            PageId root;
            await using (var database = await PageDatabase.CreateAsync(path))
            {
                await using var pool = new BufferPool(database, 2, leaveOpen: true);
                var table = await TableHeap.CreateAsync(pool, database);
                root = table.RootPageId;
                id = await table.InsertAsync(Enumerable.Repeat((byte)42, 1000).ToArray());
                await pool.FlushAllAsync();
            }
            await using (var reopened = await PageDatabase.OpenAsync(path))
            await using (var pool = new BufferPool(reopened, 2, leaveOpen: true))
            {
                var table = await TableHeap.OpenAsync(root, pool, reopened);
                var lookup = await table.ReadAsync(id);
                lookup.Result.Should().Be(TableHeapLookupResult.Found);
                lookup.Row.ToArray().Should().OnlyContain(value => value == 42).And.HaveCount(1000);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static (string Directory, string Path) NewPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sql-heap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return (directory, Path.Combine(directory, "database.sse"));
    }
}
