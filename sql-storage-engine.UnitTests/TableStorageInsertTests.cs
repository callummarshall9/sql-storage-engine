using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Catalog;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;
using sql_storage_engine.Tables;

namespace sql_storage_engine.UnitTests;

public sealed class TableStorageInsertTests
{
    [Test]
    public async Task SuccessfulInsert_CreatesHeapRowAndEveryIndexEntryAndReturnsResolvableId()
    {
        await using var fixture = await Fixture.CreateAsync();
        var row = new Row([SqlValue.Integer(7), SqlValue.Text(new string('x', 100))]);
        var id = await fixture.Table.InsertAsync(row);
        var resolved = await fixture.Table.TryGetAsync(id);
        resolved.Found.Should().BeTrue();
        resolved.Row!.Values.Should().BeEquivalentTo(row.Values);
        foreach (var index in fixture.Indexes)
            (await index.Tree.FindAsync(CatalogIndexKey.Encode(row, fixture.Definition, index.Definition))).Should().Equal(id);
    }

    [Test]
    public async Task ValidationFailure_ChangesNoHeapIndexOrOverflowStorage()
    {
        await using var fixture = await Fixture.CreateAsync();
        await ((Func<Task>)(async () => await fixture.Table.InsertAsync(
            new Row([SqlValue.Null, SqlValue.Text("bad")])))).Should().ThrowAsync<ArgumentException>();
        var rows = new List<RowId>();
        await foreach (var entry in fixture.Heap.ScanAsync()) rows.Add(entry.RowId);
        rows.Should().BeEmpty();
        foreach (var index in fixture.Indexes)
            (await index.Tree.FindAsync(new IndexKey(new byte[] { 1 }))).Should().BeEmpty();
    }

    [Test]
    public async Task IndexFailure_CompensatesHeapIndexesAndOverflowAndReportsNoLeaks()
    {
        await using var fixture = await Fixture.CreateAsync(uniqueFirst: true);
        var original = new Row([SqlValue.Integer(7), SqlValue.Text(new string('a', 100))]);
        await fixture.Table.InsertAsync(original);
        var duplicate = new Row([SqlValue.Integer(7), SqlValue.Text(new string('b', 100))]);

        var assertion = await ((Func<Task>)(async () => await fixture.Table.InsertAsync(duplicate))).Should()
            .ThrowAsync<TableMutationException>();

        assertion.Which.InnerException.Should().BeOfType<DuplicateIndexKeyException>();
        assertion.Which.UnreclaimedPageIds.Should().BeEmpty();
        var rows = new List<RowId>();
        await foreach (var entry in fixture.Heap.ScanAsync()) rows.Add(entry.RowId);
        rows.Should().HaveCount(1);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        private readonly InMemoryPageStore _pages;
        private readonly BufferPool _pool;
        private Fixture(InMemoryPageStore pages, BufferPool pool, CatalogTable definition, TableHeap heap,
            TableIndex[] indexes, TableStorage table)
        { _pages = pages; _pool = pool; Definition = definition; Heap = heap; Indexes = indexes; Table = table; }
        public CatalogTable Definition { get; }
        public TableHeap Heap { get; }
        public TableIndex[] Indexes { get; }
        public TableStorage Table { get; }
        internal InMemoryPageStore Pages => _pages;
        internal BufferPool Pool => _pool;

        public static async Task<Fixture> CreateAsync(bool uniqueFirst = false, int inlineThreshold = 16)
        {
            var pages = new InMemoryPageStore();
            var pool = new BufferPool(pages, 16, leaveOpen: true);
            var heap = await TableHeap.CreateAsync(pool, pages);
            var definition = new CatalogTable(new TableId(1), "items", 1, heap.RootPageId,
                [new CatalogColumn(new ColumnId(1), "key", SqlType.Integer, false),
                 new CatalogColumn(new ColumnId(2), "value", SqlType.Text, true)]);
            var indexes = new List<TableIndex>();
            foreach (var (id, column, unique) in new[] { (1UL, 1UL, uniqueFirst), (2UL, 2UL, false) })
            {
                var root = await pages.AllocateAsync(PageType.BPlusTreeLeaf);
                using (var pin = await pool.GetPageAsync(root))
                { LeafIndexPageCodec.Write(pin.Memory.Span, new LeafIndexPage(root, null, null, null, [])); pin.MarkDirty(new LogSequenceNumber(0)); }
                var metadata = new CatalogIndex(new IndexId(id), $"i{id}", definition.Id, root, unique,
                    [new CatalogIndexedColumn(new ColumnId(column), SortDirection.Ascending, NullSortOrder.Last)]);
                indexes.Add(new TableIndex(metadata,
                    new PersistentBPlusTree(pool, pages, new MutableIndexRootReference(root), unique)));
            }
            var overflow = new OverflowManager(pool, pages);
            var table = new TableStorage(definition, heap, new OverflowRowCodec(overflow, inlineThreshold), overflow, indexes);
            return new Fixture(pages, pool, definition, heap, indexes.ToArray(), table);
        }
        public async ValueTask DisposeAsync() { await _pool.DisposeAsync(); await _pages.DisposeAsync(); }
    }
}
