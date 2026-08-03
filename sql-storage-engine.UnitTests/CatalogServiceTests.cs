using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class CatalogServiceTests
{
    [Test]
    public async Task CreatedTable_OpensByNameAndIdAndSurvivesReopen()
    {
        await using var pages = new InMemoryPageStore();
        await using var pool = new BufferPool(pages, 4, leaveOpen: true);
        var catalog = CatalogService.CreateEmpty(pages, pages, pool);
        var created = await catalog.CreateTableAsync("items", 7,
            [new CatalogColumn(new ColumnId(1), "value", SqlType.Text, true)]);
        catalog.RootPageId.Should().NotBeNull();
        var root = catalog.RootPageId!.Value;

        catalog.TryOpenTable("items", out var byName).Should().BeTrue();
        catalog.TryOpenTable(created.Id, out var byId).Should().BeTrue();
        byName.Should().BeEquivalentTo(byId);

        var reopened = await CatalogService.OpenAsync(root, pages, pages, pool);
        reopened.TryOpenTable("items", out var persisted).Should().BeTrue();
        persisted!.SchemaVersion.Should().Be(7);
        persisted.FirstHeapPageId.Should().Be(created.FirstHeapPageId);
        var heap = await reopened.OpenHeapAsync(persisted);
        heap.RootPageId.Should().Be(created.FirstHeapPageId);
    }

    [Test]
    public async Task DuplicateName_IsRejectedWithoutChangingPublishedCatalog()
    {
        await using var pages = new InMemoryPageStore();
        await using var pool = new BufferPool(pages, 4, leaveOpen: true);
        var catalog = CatalogService.CreateEmpty(pages, pages, pool);
        await catalog.CreateTableAsync("items", 1,
            [new CatalogColumn(new ColumnId(1), "value", SqlType.Integer, false)]);
        var root = catalog.RootPageId;

        await ((Func<Task>)(async () => await catalog.CreateTableAsync("items", 1,
            [new CatalogColumn(new ColumnId(1), "other", SqlType.Integer, false)]))).Should()
            .ThrowAsync<CatalogConflictException>();

        catalog.RootPageId.Should().Be(root);
        catalog.Tables.Should().HaveCount(1);
    }

    [Test]
    public async Task InvalidSchema_AllocatesNoPageAndPublishesNoTable()
    {
        await using var inner = new InMemoryPageStore();
        await using var allocator = new TrackingAllocator(inner);
        await using var pool = new BufferPool(allocator, 2, leaveOpen: true);
        var catalog = CatalogService.CreateEmpty(allocator, allocator, pool);

        await ((Func<Task>)(async () => await catalog.CreateTableAsync("bad", 1,
            [new CatalogColumn(new ColumnId(1), "same", SqlType.Integer, false),
             new CatalogColumn(new ColumnId(2), "same", SqlType.Integer, false)]))).Should()
            .ThrowAsync<ArgumentException>();

        allocator.AllocationCount.Should().Be(0);
        catalog.RootPageId.Should().BeNull();
        catalog.Tables.Should().BeEmpty();
    }

    [Test]
    public async Task SecondaryIndex_BuildsEveryLiveRowAndSurvivesCatalogReopen()
    {
        await using var pages = new InMemoryPageStore();
        await using var pool = new BufferPool(pages, 12, leaveOpen: true);
        var catalog = CatalogService.CreateEmpty(pages, pages, pool);
        var table = await catalog.CreateTableAsync("items", 1,
            [new CatalogColumn(new ColumnId(1), "key", SqlType.Integer, false),
             new CatalogColumn(new ColumnId(2), "value", SqlType.Text, true)]);
        var heap = await catalog.OpenHeapAsync(table);
        var schema = new TableDefinition(table.Columns.Select(column =>
            new ColumnDefinition(column.Id, column.Name, column.Type, column.IsNullable)));
        var first = await heap.InsertAsync(RowCodec.Encode(new Row([SqlValue.Integer(7), SqlValue.Text("a")]), schema));
        var second = await heap.InsertAsync(RowCodec.Encode(new Row([SqlValue.Integer(8), SqlValue.Text("b")]), schema));

        var index = await catalog.CreateIndexAsync("by_key", table.Id, false,
            [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.Last)]);
        var tree = catalog.OpenIndex(index);
        (await tree.FindAsync(CatalogIndexKey.Encode(new Row([SqlValue.Integer(7), SqlValue.Text("ignored")]), table, index)))
            .Should().Equal(first);
        (await tree.FindAsync(CatalogIndexKey.Encode(new Row([SqlValue.Integer(8), SqlValue.Null]), table, index)))
            .Should().Equal(second);

        var reopened = await CatalogService.OpenAsync(catalog.RootPageId!.Value, pages, pages, pool);
        reopened.TryOpenIndex("by_key", table.Id, out var persisted).Should().BeTrue();
        (await reopened.OpenIndex(persisted!).FindAsync(CatalogIndexKey.Encode(
            new Row([SqlValue.Integer(7), SqlValue.Null]), table, persisted!))).Should().Equal(first);
    }

    [Test]
    public async Task UniqueIndexDuplicate_FailsWithoutPublicationAndReportsCleanedPages()
    {
        await using var pages = new InMemoryPageStore();
        await using var pool = new BufferPool(pages, 8, leaveOpen: true);
        var catalog = CatalogService.CreateEmpty(pages, pages, pool);
        var table = await catalog.CreateTableAsync("items", 1,
            [new CatalogColumn(new ColumnId(1), "key", SqlType.Integer, false)]);
        var heap = await catalog.OpenHeapAsync(table);
        var schema = new TableDefinition([new ColumnDefinition(new ColumnId(1), "key", SqlType.Integer, false)]);
        await heap.InsertAsync(RowCodec.Encode(new Row([SqlValue.Integer(7)]), schema));
        await heap.InsertAsync(RowCodec.Encode(new Row([SqlValue.Integer(7)]), schema));

        var assertion = await ((Func<Task>)(async () => await catalog.CreateIndexAsync("unique_key", table.Id, true,
            [new CatalogIndexedColumn(new ColumnId(1), SortDirection.Ascending, NullSortOrder.Last)]))).Should()
            .ThrowAsync<IndexBuildException>();

        catalog.TryOpenIndex("unique_key", table.Id, out _).Should().BeFalse();
        assertion.Which.AllocatedPageIds.Should().NotBeEmpty();
        assertion.Which.UnreclaimedPageIds.Should().BeEmpty();
        foreach (var pageId in assertion.Which.AllocatedPageIds)
            await ((Func<Task>)(async () => await pages.ReadAsync(pageId, new byte[pages.PageSize]))).Should()
                .ThrowAsync<StorageResourceException>();
    }

    private sealed class TrackingAllocator(InMemoryPageStore inner) : IPageStore, IPageAllocator
    {
        public int AllocationCount { get; private set; }
        public int PageSize => inner.PageSize;
        public async ValueTask<PageId> AllocateAsync(PageType type, CancellationToken token = default)
        { AllocationCount++; return await inner.AllocateAsync(type, token); }
        public ValueTask FreeAsync(PageId id, CancellationToken token = default) => inner.FreeAsync(id, token);
        public ValueTask ReadAsync(PageId id, Memory<byte> bytes, CancellationToken token = default) => inner.ReadAsync(id, bytes, token);
        public ValueTask WriteAsync(PageId id, ReadOnlyMemory<byte> bytes, CancellationToken token = default) => inner.WriteAsync(id, bytes, token);
        public ValueTask FlushAsync(CancellationToken token = default) => inner.FlushAsync(token);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
