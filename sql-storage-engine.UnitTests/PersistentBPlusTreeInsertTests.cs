using AwesomeAssertions;
using System.Buffers.Binary;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class PersistentBPlusTreeInsertTests
{
    [Test]
    public async Task InsertWithoutSplit_PreservesOrderAndEveryDuplicateRowId()
    {
        await using var store = new InMemoryPageStore();
        var root = await WriteLeafAsync(store, Array.Empty<LeafIndexEntry>());
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var tree = new PersistentBPlusTree(pool, store, new MutableIndexRootReference(root));

        (await tree.InsertWithoutSplitAsync(Key(2), Row(20))).Should().Be(IndexInsertResult.Inserted);
        (await tree.InsertWithoutSplitAsync(Key(1), Row(10))).Should().Be(IndexInsertResult.Inserted);
        (await tree.InsertWithoutSplitAsync(Key(2), Row(21))).Should().Be(IndexInsertResult.Inserted);

        (await tree.FindAsync(Key(2))).Should().Equal(Row(20), Row(21));
        (await CollectAsync(tree.ScanAsync(new IndexRange(Key(1), Key(2))))).Select(entry => entry.Key)
            .Should().Equal(Key(1), Key(2), Key(2));
    }

    [Test]
    public async Task FullLeaf_ReturnsSplitRequiredWithoutPartialMutation()
    {
        await using var store = new InMemoryPageStore();
        var entries = new List<LeafIndexEntry>();
        for (var value = 1; ; value++)
        {
            var keyBytes = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(keyBytes, value);
            var candidate = entries.Append(new LeafIndexEntry(new IndexKey(keyBytes), Row((ulong)value + 1))).ToArray();
            if (!LeafIndexPageCodec.CanFit(store.PageSize, candidate)) break;
            entries.Add(candidate[^1]);
        }
        var root = await WriteLeafAsync(store, entries);
        var before = new byte[store.PageSize];
        await store.ReadAsync(root, before);
        await using var pool = new BufferPool(store, 1, leaveOpen: true);
        var tree = new PersistentBPlusTree(pool, store, new MutableIndexRootReference(root));

        (await tree.InsertWithoutSplitAsync(new IndexKey(new byte[] { 0xff, 0xff, 0xff, 0xff }), Row(9999)))
            .Should().Be(IndexInsertResult.SplitRequired);

        using var pin = await pool.GetPageAsync(root);
        pin.Memory.ToArray().Should().Equal(before);
    }

    [Test]
    public async Task InsertedEntryAndChangedAncestorSeparatorSurviveReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sql-index-insert-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "database.sse");
        try
        {
            PageId root;
            await using (var database = await PageDatabase.CreateAsync(path))
            {
                var left = await WriteLeafAsync(database, new[] { Entry(1, 10), Entry(2, 20) });
                var right = await WriteLeafAsync(database, new[] { Entry(5, 50), Entry(6, 60) });
                root = await database.AllocateAsync(PageType.BPlusTreeInternal);
                await WriteInternalAsync(database, root, new[] { Key(5) }, new[] { left, right });
                await RewriteLeafParentAsync(database, left, root);
                await RewriteLeafParentAsync(database, right, root);
                await using var pool = new BufferPool(database, 3, leaveOpen: true);
                var tree = new PersistentBPlusTree(pool, database, new MutableIndexRootReference(root));
                (await tree.InsertWithoutSplitAsync(Key(4), Row(40))).Should().Be(IndexInsertResult.Inserted);
                await pool.FlushAllAsync();
            }
            await using (var database = await PageDatabase.OpenAsync(path))
            await using (var pool = new BufferPool(database, 3, leaveOpen: true))
            {
                var tree = new PersistentBPlusTree(pool, database, new MutableIndexRootReference(root));
                (await tree.FindAsync(Key(4))).Should().Equal(Row(40));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    internal static IndexKey Key(byte value) => new(new[] { value });
    internal static RowId Row(ulong value) => new(new PageId(value), new SlotId(0), new SlotGeneration(0));
    internal static LeafIndexEntry Entry(byte key, ulong row) => new(Key(key), Row(row));
    internal static async Task<PageId> WriteLeafAsync(IPageStore store, IEnumerable<LeafIndexEntry> entries,
        PageId? id = null, PageId? parent = null, PageId? previous = null, PageId? next = null)
    {
        if (store is not IPageAllocator allocator) throw new ArgumentException("Store must allocate pages.");
        var pageId = id ?? await allocator.AllocateAsync(PageType.BPlusTreeLeaf);
        var bytes = new byte[store.PageSize];
        LeafIndexPageCodec.Write(bytes, new LeafIndexPage(pageId, parent, previous, next, entries.ToArray()));
        await store.WriteAsync(pageId, bytes);
        return pageId;
    }
    internal static async Task WriteInternalAsync(IPageStore store, PageId id, IndexKey[] separators, PageId[] children,
        PageId? parent = null)
    {
        var bytes = new byte[store.PageSize];
        InternalIndexPageCodec.Write(bytes, new InternalIndexPage(id, parent, separators, children));
        await store.WriteAsync(id, bytes);
    }
    internal static async Task RewriteLeafParentAsync(IPageStore store, PageId id, PageId parent)
    {
        var bytes = new byte[store.PageSize];
        await store.ReadAsync(id, bytes);
        var leaf = LeafIndexPageCodec.Read(bytes, id);
        LeafIndexPageCodec.Write(bytes, leaf with { ParentPageId = parent });
        await store.WriteAsync(id, bytes);
    }
    internal static async Task<List<LeafIndexEntry>> CollectAsync(IAsyncEnumerable<LeafIndexEntry> source)
    {
        List<LeafIndexEntry> result = [];
        await foreach (var entry in source) result.Add(entry);
        return result;
    }
}
