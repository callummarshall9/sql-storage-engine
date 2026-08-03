using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class FreeSpaceMapTests
{
    [Test]
    public void Map_ReturnsDeterministicSuitablePageAndTracksCoarseCategories()
    {
        var map = new InMemoryFreeSpaceMap(PageConstants.DefaultSize);
        map.Update(new PageId(3), 3000);
        map.Update(new PageId(1), 1000);
        map.Update(new PageId(2), 5000);

        map.FindPage(2500).Should().Be(new PageId(2));
        map.TryGetCategory(new PageId(1), out var small).Should().BeTrue();
        map.TryGetCategory(new PageId(2), out var large).Should().BeTrue();
        small.Should().Be(FreeSpaceCategory.Tiny);
        large.Should().Be(FreeSpaceCategory.Medium);
    }

    [Test]
    public async Task TableHeap_CorrectsOptimisticHintsAndRemainsCorrectWithEmptyMap()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 3, leaveOpen: true);
        var map = new InMemoryFreeSpaceMap(store.PageSize);
        var table = await TableHeap.CreateAsync(pool, store, map);
        var full = await table.InsertAsync(new byte[store.PageSize - HeapPageLayout.HeaderLength - HeapPageLayout.SlotEntryLength]);
        table.FreeSpaceMap.Update(table.RootPageId, store.PageSize);

        var next = await table.InsertAsync(new byte[] { 9 });

        next.PageId.Should().NotBe(full.PageId);
        table.FreeSpaceMap.FindPage(store.PageSize).Should().BeNull();
        table.FreeSpaceMap.Clear();
        var another = await table.InsertAsync(new byte[] { 8 });
        another.PageId.Should().Be(next.PageId);
        (await table.ReadAsync(another)).Result.Should().Be(TableHeapLookupResult.Found);
    }

    [Test]
    public async Task Rebuild_ProducesSameCategoriesAsLiveInsertUpdateDeleteAndCompaction()
    {
        await using var store = new InMemoryPageStore();
        await using var pool = new BufferPool(store, 3, leaveOpen: true);
        var map = new InMemoryFreeSpaceMap(store.PageSize);
        var table = new TableHeap((await store.AllocateAsync(PageType.Heap)), pool, store, map);
        using (var rootPin = await pool.GetPageAsync(table.RootPageId))
        {
            HeapPageLayout.Initialize(rootPin.Memory.Span, table.RootPageId);
            rootPin.MarkDirty(new LogSequenceNumber(0));
        }
        await table.RebuildFreeSpaceMapAsync();
        var first = await table.InsertAsync(new byte[3000]);
        var second = await table.InsertAsync(new byte[3000]);
        await table.UpdateAsync(first, new byte[1000]);
        await table.DeleteAsync(second);
        await table.CompactPageAsync(second.PageId);
        var pageIds = new[] { first.PageId, second.PageId }.Distinct().ToArray();
        var before = pageIds.ToDictionary(id => id, id => GetCategory(map, id));

        await table.RebuildFreeSpaceMapAsync();

        foreach (var pageId in pageIds) GetCategory(map, pageId).Should().Be(before[pageId]);
    }

    private static FreeSpaceCategory GetCategory(InMemoryFreeSpaceMap map, PageId pageId)
    {
        map.TryGetCategory(pageId, out var category).Should().BeTrue();
        return category;
    }
}
