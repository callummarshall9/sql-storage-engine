using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class PersistentBPlusTreeLeafSplitTests
{
    [Test]
    public async Task RootLeafSplit_CreatesBalancedLinkedLeavesAndValidInternalRoot()
    {
        await using var store = new InMemoryPageStore();
        var entries = FillLeaf(store.PageSize);
        var oldRoot = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, entries);
        var rootReference = new MutableIndexRootReference(oldRoot);
        await using var pool = new BufferPool(store, 4, leaveOpen: true);
        var tree = new PersistentBPlusTree(pool, store, rootReference);
        var inserted = new LeafIndexEntry(IntEntryKey(entries.Count + 1), PersistentBPlusTreeInsertTests.Row(9999));

        await tree.InsertAsync(inserted.Key, inserted.RowId);

        rootReference.RootPageId.Should().NotBe(oldRoot);
        InternalIndexPage root;
        using (var rootPin = await pool.GetPageAsync(rootReference.RootPageId))
            root = InternalIndexPageCodec.Read(rootPin.Memory.Span, rootReference.RootPageId);
        root.Children.Should().HaveCount(2);
        LeafIndexPage left;
        LeafIndexPage right;
        using (var pin = await pool.GetPageAsync(root.Children[0])) left = LeafIndexPageCodec.Read(pin.Memory.Span, root.Children[0]);
        using (var pin = await pool.GetPageAsync(root.Children[1])) right = LeafIndexPageCodec.Read(pin.Memory.Span, root.Children[1]);
        left.Entries.Count.Should().BeGreaterThanOrEqualTo((entries.Count + 1) / 2);
        right.Entries.Count.Should().BeGreaterThanOrEqualTo((entries.Count + 1) / 2);
        left.NextPageId.Should().Be(right.PageId);
        right.PreviousPageId.Should().Be(left.PageId);
        left.ParentPageId.Should().Be(root.PageId);
        right.ParentPageId.Should().Be(root.PageId);
        left.Entries.Concat(right.Entries).Should().HaveCount(entries.Count + 1)
            .And.ContainSingle(entry => entry.RowId == inserted.RowId);
        root.Separators.Single().Should().Be(right.Entries[0].Key);
    }

    [Test]
    public async Task LeafSplitAndNewRootSurviveFlushAndReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sql-index-leaf-split-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "database.sse");
        try
        {
            PageId root;
            IndexKey insertedKey;
            await using (var database = await PageDatabase.CreateAsync(path))
            {
                var entries = FillLeaf(database.PageSize);
                var oldRoot = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database, entries);
                var reference = new MutableIndexRootReference(oldRoot);
                await using var pool = new BufferPool(database, 4, leaveOpen: true);
                var tree = new PersistentBPlusTree(pool, database, reference);
                insertedKey = IntEntryKey(entries.Count + 1);
                await tree.InsertAsync(insertedKey, PersistentBPlusTreeInsertTests.Row(9999));
                root = reference.RootPageId;
                await pool.FlushAllAsync();
            }
            await using (var database = await PageDatabase.OpenAsync(path))
            await using (var pool = new BufferPool(database, 4, leaveOpen: true))
            {
                var tree = new PersistentBPlusTree(pool, database, new MutableIndexRootReference(root));
                (await tree.FindAsync(insertedKey)).Should().Equal(PersistentBPlusTreeInsertTests.Row(9999));
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    private static List<LeafIndexEntry> FillLeaf(int pageSize)
    {
        List<LeafIndexEntry> entries = [];
        for (var value = 1; ; value++)
        {
            var entry = new LeafIndexEntry(IntEntryKey(value), PersistentBPlusTreeInsertTests.Row((ulong)value + 1));
            if (!LeafIndexPageCodec.CanFit(pageSize, entries.Append(entry))) return entries;
            entries.Add(entry);
        }
    }
    private static IndexKey IntEntryKey(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return new IndexKey(bytes);
    }
}
