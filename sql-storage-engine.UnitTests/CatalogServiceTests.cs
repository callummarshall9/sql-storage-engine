using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
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
