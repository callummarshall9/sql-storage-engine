// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Exposures.Catalog;

namespace sql_storage_engine.UnitTests;
public sealed class TableStorageInsertTests
{
    [Test]
    public async Task SuccessfulInsert_CreatesHeapRowAndEveryIndexEntryAndReturnsResolvableId()
    {
        await using var fixture = await Fixture.CreateAsync();
        var row = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('x', 100))]);
        var id = await fixture.Table.InsertAsync(row);
        var resolved = await fixture.Table.TryGetAsync(id);
        resolved.Found.Should().BeTrue();
        resolved.Row!.Values.Should().BeEquivalentTo(row.Values);
        foreach (var index in fixture.Indexes)
            (await index.ReadTree().FindAsync(sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.Encode(row, fixture.Definition, index.ReadDefinition()))).Should().Equal(id);
    }

    [Test]
    public async Task ValidationFailure_ChangesNoHeapIndexOrOverflowStorage()
    {
        await using var fixture = await Fixture.CreateAsync();
        await ((Func<Task>)(async () => await fixture.Table.InsertAsync(RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.ReadNullSqlValue(), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue("bad")])))).Should().ThrowAsync<ArgumentException>();
        var rows = new List<RowId>();
        await foreach (var entry in fixture.Heap.ScanAsync())
            rows.Add(entry.RowId);
        rows.Should().BeEmpty();
        foreach (var index in fixture.Indexes)
            (await index.ReadTree().FindAsync(sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: new byte[] { 1 }))).Should().BeEmpty();
    }

    [Test]
    public async Task IndexFailure_CompensatesHeapIndexesAndOverflowAndReportsNoLeaks()
    {
        await using var fixture = await Fixture.CreateAsync(uniqueFirst: true);
        var original = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('a', 100))]);
        await fixture.Table.InsertAsync(original);
        var duplicate = RowExposure.CreateRow([sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateIntegerSqlValue(7), sql_storage_engine.Exposures.Rows.SqlValueExposure.CreateTextSqlValue(new string ('b', 100))]);
        var assertion = await ((Func<Task>)(async () => await fixture.Table.InsertAsync(duplicate))).Should().ThrowAsync<TableMutationException>();
        assertion.Which.InnerException.Should().BeOfType<DuplicateIndexKeyException>();
        assertion.Which.UnreclaimedPageIds.Should().BeEmpty();
        var rows = new List<RowId>();
        await foreach (var entry in fixture.Heap.ScanAsync())
            rows.Add(entry.RowId);
        rows.Should().HaveCount(1);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure _pages;
        private readonly sql_storage_engine.Exposures.Buffers.BufferPoolExposure _pool;
        private Fixture(sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure pages, sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool, CatalogTable definition, sql_storage_engine.Exposures.Heap.TableHeapExposure heap, sql_storage_engine.Exposures.Tables.TableIndexExposure[] indexes, sql_storage_engine.Exposures.Tables.TableStorageExposure table)
        {
            _pages = pages;
            _pool = pool;
            Definition = definition;
            Heap = heap;
            Indexes = indexes;
            Table = table;
        }

        public CatalogTable Definition { get; }
        public sql_storage_engine.Exposures.Heap.TableHeapExposure Heap { get; }
        public sql_storage_engine.Exposures.Tables.TableIndexExposure[] Indexes { get; }
        public sql_storage_engine.Exposures.Tables.TableStorageExposure Table { get; }
        internal sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure Pages => _pages;
        internal sql_storage_engine.Exposures.Buffers.BufferPoolExposure Pool => _pool;

        public static async Task<Fixture> CreateAsync(bool uniqueFirst = false, int inlineThreshold = 16)
        {
            sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure pages = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
            sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(pages, 16, leaveOpen: true);
            var heap = await sql_storage_engine.Exposures.Heap.TableHeapExposure.CreateTableHeapExposureAsync(pool,pages);
            var definition = sql_storage_engine.Exposures.Catalog.CatalogTableExposure.CreateCatalogTable(new sql_storage_engine.Models.Identifiers.TableId { Value = 1 }, "items", 1, heap.ReadRootPageId(), [sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 1 }, "key", sql_storage_engine.Exposures.Rows.SqlTypeExposure.ReadSqlTypeInt(), false), sql_storage_engine.Exposures.Catalog.CatalogColumnExposure.CreateCatalogColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = 2 }, "value", sql_storage_engine.Exposures.Rows.SqlTypeExposure.CreateVarCharSqlType(8000), true)]);
            var indexes = new List<sql_storage_engine.Exposures.Tables.TableIndexExposure>();
            foreach (var(id, column, unique)in new[]
            {
                (1UL, 1UL, uniqueFirst),
                (2UL, 2UL, false)
            }

            )
            {
                var root = await pages.AllocateAsync(PageType.BPlusTreeLeaf);
                using (var pin = await pool.GetPageAsync(root))
                {
                    sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(pin.ReadMemory().Span,new LeafIndexPage { PageId = root, ParentPageId = null, PreviousPageId = null, NextPageId = null, Entries = [] });
                    pin.MarkDirty(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 });
                }

                var metadata = sql_storage_engine.Exposures.Catalog.CatalogIndexExposure.CreateCatalogIndex(indexId: new sql_storage_engine.Models.Identifiers.IndexId { Value = id },name: $"i{id}",tableId: definition.Id,pageId: root,isUnique: unique,columns: [sql_storage_engine.Exposures.Catalog.CatalogIndexedColumnExposure.CreateCatalogIndexedColumn(new sql_storage_engine.Models.Identifiers.ColumnId { Value = column },SortDirection.Ascending,NullSortOrder.Last)]);
                indexes.Add(new sql_storage_engine.Exposures.Tables.TableIndexExposure(catalogIndex:metadata,new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,pages,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root),unique)));
            }

            var overflow = new sql_storage_engine.Exposures.Overflow.OverflowChainExposure(pool, pages);
            var table = new sql_storage_engine.Exposures.Tables.TableStorageExposure(catalogTable:definition,heap:heap,rowCodec:new sql_storage_engine.Exposures.Rows.OverflowRowCodecExposure(overflow,inlineThreshold),indexes:indexes);
            return new Fixture(pages, pool, definition, heap, indexes.ToArray(), table);
        }

        public async ValueTask DisposeAsync()
        {
            await _pool.DisposeAsync();
            await _pages.DisposeAsync();
        }
    }
}