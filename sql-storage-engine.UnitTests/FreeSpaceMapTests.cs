using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class FreeSpaceMapTests
{
    [Test]
    public void Map_ReturnsDeterministicSuitablePageAndTracksCoarseCategories()
    {
        sql_storage_engine.Exposures.Heap.FreeSpaceMapExposure map = new sql_storage_engine.Exposures.Heap.FreeSpaceMapExposure(sql_storage_engine.Models.Pages.PageConstants.DefaultSize);
        map.Update(new sql_storage_engine.Models.Identifiers.PageId { Value = 3 },3000);
        map.Update(new sql_storage_engine.Models.Identifiers.PageId { Value = 1 },1000);
        map.Update(new sql_storage_engine.Models.Identifiers.PageId { Value = 2 },5000);
        map.FindPage(2500).Should().Be(new sql_storage_engine.Models.Identifiers.PageId { Value = 2 });
        map.TryGetCategory(new sql_storage_engine.Models.Identifiers.PageId { Value = 1 },out var small).Should().BeTrue();
        map.TryGetCategory(new sql_storage_engine.Models.Identifiers.PageId { Value = 2 },out var large).Should().BeTrue();
        small.Should().Be(FreeSpaceCategory.Tiny);
        large.Should().Be(FreeSpaceCategory.Medium);
    }

    [Test]
    public async Task TableHeap_CorrectsOptimisticHintsAndRemainsCorrectWithEmptyMap()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 3, leaveOpen: true);
        sql_storage_engine.Exposures.Heap.FreeSpaceMapExposure map = new sql_storage_engine.Exposures.Heap.FreeSpaceMapExposure(store.GetPageSize());
        var table = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool,store,map);
        var full = await table.InsertAsync(new byte[store.GetPageSize() - sql_storage_engine.Models.Heap.HeapPageFormat.HeaderLength - sql_storage_engine.Models.Heap.HeapPageFormat.SlotEntryLength]);
        table.ReadFreeSpaceMap().Update(table.ReadRootPageId(),store.GetPageSize());
        var next = await table.InsertAsync(new byte[] { 9 });
        next.PageId.Should().NotBe(full.PageId);
        table.ReadFreeSpaceMap().FindPage(store.GetPageSize()).Should().BeNull();
        table.ReadFreeSpaceMap().Clear();
        var another = await table.InsertAsync(new byte[] { 8 });
        another.PageId.Should().Be(next.PageId);
        (await table.ReadAsync(another)).Result.Should().Be(TableHeapLookupResult.Found);
    }

    [Test]
    public async Task Rebuild_ProducesSameCategoriesAsLiveInsertUpdateDeleteAndCompaction()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 3, leaveOpen: true);
        sql_storage_engine.Exposures.Heap.FreeSpaceMapExposure map = new sql_storage_engine.Exposures.Heap.FreeSpaceMapExposure(store.GetPageSize());
        var table = new sql_storage_engine.Exposures.Heap.TableHeapExposure((await store.AllocateAsync(PageType.Heap)),pool,store,map);
        using (var rootPin = await pool.GetPageAsync(table.ReadRootPageId()))
        {
            sql_storage_engine.Exposures.Heap.HeapPageLayoutExposure.InitializeHeapPageLayout(rootPin.ReadMemory().Span,(table.ReadRootPageId()).Value);
            rootPin.MarkDirty(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 });
        }

        await table.RebuildFreeSpaceMapAsync();
        var first = await table.InsertAsync(new byte[3000]);
        var second = await table.InsertAsync(new byte[3000]);
        await table.UpdateAsync(first,new byte[1000]);
        await table.DeleteAsync(second);
        await table.CompactPageAsync(second.PageId);
        var pageIds = new[]
        {
            first.PageId,
            second.PageId
        }.Distinct().ToArray();
        var before = pageIds.ToDictionary(id => id, id => GetCategory(map, id));
        await table.RebuildFreeSpaceMapAsync();
        foreach (var pageId in pageIds)
            GetCategory(map, pageId).Should().Be(before[pageId]);
    }

    private static FreeSpaceCategory GetCategory(sql_storage_engine.Exposures.Heap.FreeSpaceMapExposure map, PageId pageId)
    {
        map.TryGetCategory(pageId,out var category).Should().BeTrue();
        return category;
    }
}