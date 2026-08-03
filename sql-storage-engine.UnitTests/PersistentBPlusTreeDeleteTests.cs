using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class PersistentBPlusTreeDeleteTests
{
    [Test]
    public async Task Delete_WithCancellation_DoesNotMutateRootLeaf()
    {
        await using var store = new InMemoryPageStore();
        var root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, new[] { Entry(1, 10), Entry(2, 20) });
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var tree = new PersistentBPlusTree(pool, store, new MutableIndexRootReference(root));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await tree.DeleteAsync(Key(1), Row(10), cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        (await tree.FindAsync(Key(1))).Should().Equal(Row(10));
    }

    [Test]
    public async Task LeafMerge_ContractsRootMaintainsLinksAndReportsRetiredPages()
    {
        await using var store = new InMemoryPageStore();
        var left = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, new[] { Entry(1, 10), Entry(2, 20) });
        var right = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, new[] { Entry(3, 30), Entry(4, 40) });
        var root = await store.AllocateAsync(PageType.BPlusTreeInternal);
        await RewriteLeaf(store, left, root, null, right);
        await RewriteLeaf(store, right, root, left, null);
        await PersistentBPlusTreeInsertTests.WriteInternalAsync(store, root, new[] { Key(3) }, new[] { left, right });
        await using var pool = new BufferPool(store, 4, leaveOpen: true);
        var rootReference = new MutableIndexRootReference(root);
        var tree = new PersistentBPlusTree(pool, store, rootReference);

        var result = await tree.DeleteAsync(Key(1), Row(10));

        result.Removed.Should().BeTrue();
        rootReference.RootPageId.Should().Be(left);
        result.RetiredPageIds.Should().BeEquivalentTo(new[] { right, root });
        var merged = await ReadLeaf(pool, left);
        merged.ParentPageId.Should().BeNull();
        merged.PreviousPageId.Should().BeNull();
        merged.NextPageId.Should().BeNull();
        merged.Entries.Select(entry => entry.Key).Should().Equal(Key(2), Key(3), Key(4));
    }

    [Test]
    public async Task Remove_DeletesOnlyRequestedDuplicateAndMissingPairDoesNotMutatePages()
    {
        await using var store = new InMemoryPageStore();
        var root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store,
            new[] { Entry(1, 10), Entry(2, 20), Entry(2, 21), Entry(3, 30) });
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var tree = new PersistentBPlusTree(pool, store, new MutableIndexRootReference(root));

        (await tree.RemoveAsync(Key(2), Row(20))).Should().BeTrue();
        (await tree.FindAsync(Key(2))).Should().Equal(Row(21));
        using var beforePin = await pool.GetPageAsync(root);
        var before = beforePin.Memory.ToArray();
        beforePin.Dispose();
        (await tree.RemoveAsync(Key(2), Row(999))).Should().BeFalse();
        using var afterPin = await pool.GetPageAsync(root);
        afterPin.Memory.ToArray().Should().Equal(before);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task UnderfilledLeaf_BorrowsFromLeftOrRightAndPersistsSeparators(bool borrowLeft)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sql-index-borrow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "database.sse");
        try
        {
            PageId root;
            PageId middle;
            await using (var database = await PageDatabase.CreateAsync(path))
            {
                var left = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database,
                    borrowLeft ? new[] { Entry(1, 10), Entry(2, 20), Entry(3, 30) } : new[] { Entry(1, 10), Entry(2, 20) });
                middle = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database,
                    new[] { Entry(4, 40), Entry(5, 50) });
                var right = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database,
                    borrowLeft ? new[] { Entry(6, 60), Entry(7, 70) } : new[] { Entry(6, 60), Entry(7, 70), Entry(8, 80) });
                root = await database.AllocateAsync(PageType.BPlusTreeInternal);
                await RewriteLeaf(database, left, root, null, middle);
                await RewriteLeaf(database, middle, root, left, right);
                await RewriteLeaf(database, right, root, middle, null);
                await PersistentBPlusTreeInsertTests.WriteInternalAsync(database, root,
                    new[] { Key(4), Key(6) }, new[] { left, middle, right });
                await using var pool = new BufferPool(database, 4, leaveOpen: true);
                var tree = new PersistentBPlusTree(pool, database, new MutableIndexRootReference(root));
                (await tree.RemoveAsync(Key(4), Row(40))).Should().BeTrue();
                await pool.FlushAllAsync();
            }
            await using (var database = await PageDatabase.OpenAsync(path))
            await using (var pool = new BufferPool(database, 4, leaveOpen: true))
            {
                LeafIndexPage leaf;
                using (var pin = await pool.GetPageAsync(middle)) leaf = LeafIndexPageCodec.Read(pin.Memory.Span, middle);
                leaf.Entries.Should().HaveCount(2);
                InternalIndexPage node;
                using (var pin = await pool.GetPageAsync(root)) node = InternalIndexPageCodec.Read(pin.Memory.Span, root);
                node.Separators[0].Should().Be(leaf.Entries[0].Key);
                node.Separators[1].Should().Be((await ReadLeaf(pool, node.Children[2])).Entries[0].Key);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task FixedSeedInsertDeleteAndReopen_AgreesWithReferenceAndRebalancesInternalPages()
    {
        const int seed = 74021;
        var directory = Path.Combine(Path.GetTempPath(), $"sql-index-delete-random-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "database.sse");
        var expected = Enumerable.Range(0, 48).ToDictionary(value => value, value => Row((ulong)value + 1));
        var random = new Random(seed);
        var deletionOrder = expected.Keys.OrderBy(_ => random.Next()).Take(32).ToArray();
        PageId reopenedRoot;
        List<PageId> retired = [];
        try
        {
            await using (var database = await PageDatabase.CreateAsync(path))
            {
                var initialRoot = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database, []);
                var rootReference = new MutableIndexRootReference(initialRoot);
                await using var pool = new BufferPool(database, 8, leaveOpen: true);
                var tree = new PersistentBPlusTree(pool, database, rootReference);
                foreach (var pair in expected) await tree.InsertAsync(LargeKey(pair.Key), pair.Value);
                foreach (var value in deletionOrder)
                {
                    var result = await tree.DeleteAsync(LargeKey(value), expected[value]);
                    result.Removed.Should().BeTrue($"fixed seed {seed} must delete value {value}");
                    retired.AddRange(result.RetiredPageIds);
                    expected.Remove(value);
                }
                reopenedRoot = rootReference.RootPageId;
                await pool.FlushAllAsync();
            }

            retired.Should().NotBeEmpty($"fixed seed {seed} must exercise merge retirement");
            await using var reopened = await PageDatabase.OpenAsync(path);
            await using var reopenedPool = new BufferPool(reopened, 8, leaveOpen: true);
            var reopenedTree = new PersistentBPlusTree(reopenedPool, reopened, new MutableIndexRootReference(reopenedRoot));
            var actual = new List<LeafIndexEntry>();
            await foreach (var entry in reopenedTree.ScanAsync(new IndexRange(LargeKey(0), LargeKey(255))))
                actual.Add(entry);
            actual.Select(entry => entry.RowId).Should().Equal(expected.OrderBy(pair => pair.Key).Select(pair => pair.Value),
                $"fixed seed {seed} must survive close and reopen");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static async Task RewriteLeaf(IPageStore store, PageId id, PageId parent, PageId? previous, PageId? next)
    {
        var bytes = new byte[store.PageSize];
        await store.ReadAsync(id, bytes);
        var leaf = LeafIndexPageCodec.Read(bytes, id);
        LeafIndexPageCodec.Write(bytes, leaf with { ParentPageId = parent, PreviousPageId = previous, NextPageId = next });
        await store.WriteAsync(id, bytes);
    }
    private static async Task<LeafIndexPage> ReadLeaf(BufferPool pool, PageId id)
    {
        using var pin = await pool.GetPageAsync(id);
        return LeafIndexPageCodec.Read(pin.Memory.Span, id);
    }
    private static IndexKey Key(byte value) => PersistentBPlusTreeInsertTests.Key(value);
    private static RowId Row(ulong value) => PersistentBPlusTreeInsertTests.Row(value);
    private static LeafIndexEntry Entry(byte key, ulong row) => PersistentBPlusTreeInsertTests.Entry(key, row);
    private static IndexKey LargeKey(int value)
    {
        var bytes = new byte[900];
        bytes[0] = (byte)(value >> 8);
        bytes[1] = (byte)value;
        return new IndexKey(bytes);
    }
}
