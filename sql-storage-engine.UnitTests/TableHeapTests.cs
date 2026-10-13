using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class TableHeapTests
{
    [Test]
    public async Task InsertAcrossPages_ReturnsRetrievableCompleteRowIdsAndPreservesChain()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 3, leaveOpen: true);
        var table = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool,store);
        var rows = Enumerable.Range(0, 5).Select(index => Enumerable.Repeat((byte)index, 3000).ToArray()).ToArray();
        var ids = new List<RowId>();
        foreach (var row in rows)
            ids.Add(await table.InsertAsync(row));
        ids.Select(id => id.PageId).Distinct().Count().Should().BeGreaterThan(1);
        for (var index = 0; index < rows.Length; index++)
        {
            var lookup = await table.ReadAsync(ids[index]);
            lookup.Result.Should().Be(TableHeapLookupResult.Found);
            lookup.Row.ToArray().Should().Equal(rows[index]);
        }

        pool.ReadPinnedPageCount().Should().Be(0);
    }

    [Test]
    public async Task Lookup_DistinguishesUnknownPageSlotDeletedAndStaleGeneration()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 2, leaveOpen: true);
        var table = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool,store);
        var id = await table.InsertAsync(new byte[] { 1 });
        (await table.ReadAsync(id with { PageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 999 } })).Result.Should().Be(TableHeapLookupResult.UnknownPage);
        (await table.ReadAsync(id with { SlotId = new sql_storage_engine.Models.Identifiers.SlotId { Value = 999 } })).Result.Should().Be(TableHeapLookupResult.UnknownSlot);
        (await table.ReadAsync(id with { Generation = new sql_storage_engine.Models.Identifiers.SlotGeneration { Value = id.Generation.Value + 1 } })).Result.Should().Be(TableHeapLookupResult.StaleGeneration);
        using (var pin = await pool.GetPageAsync(id.PageId))
        {
            sql_storage_engine.Exposures.Heap.HeapPageExposure.BindHeapPage(pin.ReadMemory(),id.PageId).Delete(id.SlotId,id.Generation).Should().BeTrue();
            pin.MarkDirty(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 });
        }

        (await table.ReadAsync(id)).Result.Should().Be(TableHeapLookupResult.Deleted);
    }

    [Test]
    public async Task AllocationFailure_ReleasesEveryPagePin()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using var faulting = new FaultInjectingPageStore(store, store);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(faulting, 2, leaveOpen: true);
        var table = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool,faulting);
        await table.InsertAsync(new byte[store.GetPageSize() - sql_storage_engine.Models.Heap.HeapPageFormat.HeaderLength - sql_storage_engine.Models.Heap.HeapPageFormat.SlotEntryLength]);
        faulting.FailOn = FaultInjectingPageStore.Operation.Allocate;
        await ((Func<Task>)(async () => await table.InsertAsync(new byte[] { 2 }))).Should().ThrowAsync<IOException>();
        pool.ReadPinnedPageCount().Should().Be(0);
        faulting.FailOn = FaultInjectingPageStore.Operation.None;
    }

    [Test]
    public async Task RowsSurviveFlushCloseAndReopen()
    {
        var(directory, path) = NewPath();
        try
        {
            RowId id;
            PageId root;
            await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 2, leaveOpen: true);
                var table = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool,database);
                root = table.ReadRootPageId();
                id = await table.InsertAsync(Enumerable.Repeat((byte)42, 1000).ToArray());
                await pool.FlushAllAsync();
            }

            await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure reopened = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path))
            await using (sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(reopened, 2, leaveOpen: true))
            {
                var table = await sql_storage_engine.Exposures.Heap.TableHeapExposure.OpenAsync(root,pool,reopened);
                var lookup = await table.ReadAsync(id);
                lookup.Result.Should().Be(TableHeapLookupResult.Found);
                lookup.Row.ToArray().Should().OnlyContain(value => value == 42).And.HaveCount(1000);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static (string Directory, string Path) NewPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sql-heap-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return (directory, Path.Combine(directory, "database.sse"));
    }
}