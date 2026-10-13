using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

namespace sql_storage_engine.UnitTests;
public sealed class CatalogServiceTests
{
    [Test]
    public async Task CreatedTable_OpensByNameAndIdAndSurvivesReopen()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure pages = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(pages, 4, leaveOpen: true);
        sql_storage_engine.Exposures.Catalog.CatalogSessionExposure catalog = sql_storage_engine.Exposures.Catalog.CatalogSessionExposure.CreateCatalogSessionExposure(configuration:CatalogSessionTestResources.CreateStorageSessionConfiguration(pageStore:pages,allocator:pages,bufferPool:pool,generation:null));
        var created = await catalog.CreateCatalogTableAsync(name:"items",schemaVersion:7,columns:[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(4000), true)]);
        catalog.ReadRootPageId().Should().NotBeNull();
        var root = catalog.ReadRootPageId()!.Value;
        catalog.TryOpenTable(name:"items",catalogTable:out var byName).Should().BeTrue();
        catalog.TryOpenTable(tableId:created.Id,catalogTable:out var byId).Should().BeTrue();
        byName.Should().BeEquivalentTo(byId);
        sql_storage_engine.Exposures.Catalog.CatalogSessionExposure reopened = await sql_storage_engine.Exposures.Catalog.CatalogSessionExposure.OpenCatalogSessionExposureAsync(pageId:root,configuration:CatalogSessionTestResources.CreateStorageSessionConfiguration(pageStore:pages,allocator:pages,bufferPool:pool,generation:null));
        reopened.TryOpenTable(name:"items",catalogTable:out var persisted).Should().BeTrue();
        persisted!.SchemaVersion.Should().Be(7);
        persisted.FirstHeapPageId.Should().Be(created.FirstHeapPageId);
        var heap = new sql_storage_engine.Exposures.Heap.TableHeapExposure(tableHeapBinding:await reopened.OpenTableHeapBindingAsync(catalogTable:persisted));
        heap.ReadRootPageId().Should().Be(created.FirstHeapPageId);
    }

    [Test]
    public async Task DuplicateName_IsRejectedWithoutChangingPublishedCatalog()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure pages = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(pages, 4, leaveOpen: true);
        sql_storage_engine.Exposures.Catalog.CatalogSessionExposure catalog = sql_storage_engine.Exposures.Catalog.CatalogSessionExposure.CreateCatalogSessionExposure(configuration:CatalogSessionTestResources.CreateStorageSessionConfiguration(pageStore:pages,allocator:pages,bufferPool:pool,generation:null));
        await catalog.CreateCatalogTableAsync(name:"items",schemaVersion:1,columns:[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)]);
        var root = catalog.ReadRootPageId();
        await ((Func<Task>)(async () => await catalog.CreateCatalogTableAsync(name:"items",schemaVersion:1,columns:[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "other", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)]))).Should().ThrowAsync<CatalogConflictException>();
        catalog.ReadRootPageId().Should().Be(root);
        catalog.ReadCatalogTables().Should().HaveCount(1);
    }

    [Test]
    public async Task InvalidSchema_AllocatesNoPageAndPublishesNoTable()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure inner = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using var allocator = new TrackingAllocator(inner);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(allocator, 2, leaveOpen: true);
        sql_storage_engine.Exposures.Catalog.CatalogSessionExposure catalog = sql_storage_engine.Exposures.Catalog.CatalogSessionExposure.CreateCatalogSessionExposure(configuration:CatalogSessionTestResources.CreateStorageSessionConfiguration(pageStore:allocator,allocator:allocator,bufferPool:pool,generation:null));
        await ((Func<Task>)(async () => await catalog.CreateCatalogTableAsync(name:"bad",schemaVersion:1,columns:[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "same", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "same", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)]))).Should().ThrowAsync<ArgumentException>();
        allocator.AllocationCount.Should().Be(0);
        catalog.ReadRootPageId().Should().BeNull();
        catalog.ReadCatalogTables().Should().BeEmpty();
    }

    [Test]
    public async Task SecondaryIndex_BuildsEveryLiveRowAndSurvivesCatalogReopen()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure pages = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(pages, 12, leaveOpen: true);
        sql_storage_engine.Exposures.Catalog.CatalogSessionExposure catalog = sql_storage_engine.Exposures.Catalog.CatalogSessionExposure.CreateCatalogSessionExposure(configuration:CatalogSessionTestResources.CreateStorageSessionConfiguration(pageStore:pages,allocator:pages,bufferPool:pool,generation:null));
        var table = await catalog.CreateCatalogTableAsync(name:"items",schemaVersion:1,columns:[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "key", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateNVarCharSqlType(4000), true)]);
        var heap = new sql_storage_engine.Exposures.Heap.TableHeapExposure(tableHeapBinding:await catalog.OpenTableHeapBindingAsync(catalogTable:table));
        var schema = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition(table.Columns.Select(column => sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(column.Id, column.Name, column.Type, column.IsNullable)));
        var first = await heap.InsertAsync(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("a")]),schema));
        var second = await heap.InsertAsync(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(8), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("b")]),schema));
        var index = await catalog.CreateCatalogIndexAsync(name:"by_key",tableId:table.Id,isUnique:false,columns:[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.Last)]);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(indexTreeBinding:catalog.ReadIndexTreeBinding(catalogIndex:index));
        (await tree.FindAsync(indexKey:sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("ignored")]),table,index))).Should().Equal(first);
        (await tree.FindAsync(indexKey:sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(8), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()]),table,index))).Should().Equal(second);
        sql_storage_engine.Exposures.Catalog.CatalogSessionExposure reopened = await sql_storage_engine.Exposures.Catalog.CatalogSessionExposure.OpenCatalogSessionExposureAsync(pageId:catalog.ReadRootPageId()!.Value,configuration:CatalogSessionTestResources.CreateStorageSessionConfiguration(pageStore:pages,allocator:pages,bufferPool:pool,generation:null));
        reopened.TryOpenIndex(name:"by_key",tableId:table.Id,catalogIndex:out var persisted).Should().BeTrue();
        (await new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(indexTreeBinding:reopened.ReadIndexTreeBinding(catalogIndex:persisted!)).FindAsync(indexKey:sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue()]),table,persisted!))).Should().Equal(first);
    }

    [Test]
    public async Task UniqueIndexDuplicate_FailsWithoutPublicationAndReportsCleanedPages()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure pages = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(pages, 8, leaveOpen: true);
        sql_storage_engine.Exposures.Catalog.CatalogSessionExposure catalog = sql_storage_engine.Exposures.Catalog.CatalogSessionExposure.CreateCatalogSessionExposure(configuration:CatalogSessionTestResources.CreateStorageSessionConfiguration(pageStore:pages,allocator:pages,bufferPool:pool,generation:null));
        var table = await catalog.CreateCatalogTableAsync(name:"items",schemaVersion:1,columns:[sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "key", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false)]);
        var heap = new sql_storage_engine.Exposures.Heap.TableHeapExposure(tableHeapBinding:await catalog.OpenTableHeapBindingAsync(catalogTable:table));
        var schema = sql_storage_engine.Exposures.Rows.TableDefinitionExposure.CreateTableDefinition([sql_storage_engine.Exposures.Rows.ColumnDefinitionExposure.CreateColumnDefinition(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },"key",sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(),false)]);
        await heap.InsertAsync(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7)]),schema));
        await heap.InsertAsync(sql_storage_engine.Exposures.Rows.RowCodecExposure.Encode(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7)]),schema));
        var assertion = await ((Func<Task>)(async () => await catalog.CreateCatalogIndexAsync(name:"unique_key",tableId:table.Id,isUnique:true,columns:[sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 },SortDirection.Ascending,NullSortOrder.Last)]))).Should().ThrowAsync<IndexBuildException>();
        catalog.TryOpenIndex(name:"unique_key",tableId:table.Id,catalogIndex:out _).Should().BeFalse();
        assertion.Which.AllocatedPageIds.Should().NotBeEmpty();
        assertion.Which.UnreclaimedPageIds.Should().BeEmpty();
        foreach (var pageId in assertion.Which.AllocatedPageIds)
            await ((Func<Task>)(async () => await pages.ReadAsync(pageId, new byte[pages.GetPageSize()]))).Should().ThrowAsync<StorageResourceException>();
    }

    private sealed class TrackingAllocator(sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure inner) : sql_storage_engine.Exposures.Pages.IPageStoreExposure, sql_storage_engine.Exposures.Pages.IPageAllocatorExposure
    {
        public int AllocationCount { get; private set; }

        public int GetPageSize() => inner.GetPageSize();
        public async ValueTask<PageId> AllocateAsync(PageType type, CancellationToken token = default)
        {
            AllocationCount++;
            return await inner.AllocateAsync(type, token);
        }

        public ValueTask FreeAsync(PageId id, CancellationToken token = default) => inner.FreeAsync(id, token);
        public ValueTask ReadAsync(PageId id, Memory<byte> bytes, CancellationToken token = default) => inner.ReadAsync(id, bytes, token);
        public ValueTask WriteAsync(PageId id, ReadOnlyMemory<byte> bytes, CancellationToken token = default) => inner.WriteAsync(id, bytes, token);
        public ValueTask FlushAsync(CancellationToken token = default) => inner.FlushAsync(token);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}