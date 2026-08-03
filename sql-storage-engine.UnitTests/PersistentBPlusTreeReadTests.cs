using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class PersistentBPlusTreeReadTests
{
    [Test]
    public async Task HandBuiltMultiLevelTree_SupportsDuplicatesAndBoundedRanges()
    {
        await using var store = new InMemoryPageStore();
        var fixture = await BuildFixtureAsync(store);
        await using var pool = new BufferPool(store, 3, leaveOpen: true);
        var tree = new PersistentBPlusTree(pool, store, new MutableIndexRootReference(fixture.Root));

        (await tree.FindAsync(Key(2))).Should().Equal(Row(20), Row(21), Row(22));
        (await CollectAsync(tree.ScanAsync(new IndexRange(Key(2), Key(4), false, true))))
            .Select(entry => entry.Key).Should().Equal(Key(3), Key(4));
        (await CollectAsync(tree.ScanAsync(new IndexRange(Key(2), Key(4), true, false, ScanDirection.Descending))))
            .Select(entry => entry.Key).Should().Equal(Key(3), Key(2), Key(2), Key(2));
        pool.PinnedPageCount.Should().Be(0);
    }

    [Test]
    public async Task EarlyScanDisposalAndCancellationReleasePins()
    {
        await using var store = new InMemoryPageStore();
        var fixture = await BuildFixtureAsync(store);
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var tree = new PersistentBPlusTree(pool, store, new MutableIndexRootReference(fixture.Root));
        await foreach (var _ in tree.ScanAsync(new IndexRange(Key(1), Key(9)))) break;
        pool.PinnedPageCount.Should().Be(0);
        await ((Func<Task>)(async () => await CollectAsync(tree.ScanAsync(
            new IndexRange(Key(1), Key(9)), new CancellationToken(true)))))
            .Should().ThrowAsync<OperationCanceledException>();
        pool.PinnedPageCount.Should().Be(0);
    }

    [Test]
    public async Task WrongPageTypesAndLeafCyclesAreDetected()
    {
        await using var store = new InMemoryPageStore();
        var wrong = await store.AllocateAsync(PageType.Heap);
        await using var pool = new BufferPool(store, 3, leaveOpen: true);
        var wrongTree = new PersistentBPlusTree(pool, store, new MutableIndexRootReference(wrong));
        await ((Func<Task>)(async () => await wrongTree.FindAsync(Key(1)))).Should().ThrowAsync<StorageFormatException>();

        var leaf = await store.AllocateAsync(PageType.BPlusTreeLeaf);
        var bytes = new byte[store.PageSize];
        LeafIndexPageCodec.Write(bytes, new LeafIndexPage(leaf, null, null, null, new[] { Entry(1, 1), Entry(2, 2) }));
        await store.WriteAsync(leaf, bytes);
        using (var pin = await pool.GetPageAsync(leaf))
        {
            pin.Memory.Span[LeafIndexPageCodec.NextOffset] = 1;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(
                pin.Memory.Span[(LeafIndexPageCodec.NextOffset + 1)..], leaf.Value);
            PageChecksum.WriteChecksum(pin.Memory.Span, pool.PageSize);
            pin.MarkDirty(new LogSequenceNumber(0));
        }
        var cyclic = new PersistentBPlusTree(pool, store, new MutableIndexRootReference(leaf));
        await ((Func<Task>)(async () => await CollectAsync(cyclic.ScanAsync(new IndexRange(Key(1), Key(9))))))
            .Should().ThrowAsync<StorageCorruptionException>();
    }

    private static async Task<(PageId Root, PageId[] Leaves)> BuildFixtureAsync(InMemoryPageStore store)
    {
        var leaves = new[]
        {
            await store.AllocateAsync(PageType.BPlusTreeLeaf), await store.AllocateAsync(PageType.BPlusTreeLeaf),
            await store.AllocateAsync(PageType.BPlusTreeLeaf), await store.AllocateAsync(PageType.BPlusTreeLeaf)
        };
        var leftInternal = await store.AllocateAsync(PageType.BPlusTreeInternal);
        var rightInternal = await store.AllocateAsync(PageType.BPlusTreeInternal);
        var root = await store.AllocateAsync(PageType.BPlusTreeInternal);
        var entries = new[]
        {
            new[] { Entry(1, 10), Entry(2, 20), Entry(2, 21) },
            new[] { Entry(2, 22), Entry(3, 30) },
            new[] { Entry(4, 40), Entry(5, 50) },
            new[] { Entry(6, 60), Entry(7, 70) }
        };
        for (var index = 0; index < leaves.Length; index++)
        {
            var page = new byte[store.PageSize];
            LeafIndexPageCodec.Write(page, new LeafIndexPage(leaves[index], index < 2 ? leftInternal : rightInternal,
                index == 0 ? null : leaves[index - 1], index == leaves.Length - 1 ? null : leaves[index + 1], entries[index]));
            await store.WriteAsync(leaves[index], page);
        }
        await WriteInternal(store, leftInternal, root, new[] { Key(2) }, leaves[..2]);
        await WriteInternal(store, rightInternal, root, new[] { Key(6) }, leaves[2..]);
        await WriteInternal(store, root, null, new[] { Key(4) }, new[] { leftInternal, rightInternal });
        return (root, leaves);
    }

    private static async Task WriteInternal(InMemoryPageStore store, PageId id, PageId? parent,
        IndexKey[] keys, PageId[] children)
    {
        var page = new byte[store.PageSize];
        InternalIndexPageCodec.Write(page, new InternalIndexPage(id, parent, keys, children));
        await store.WriteAsync(id, page);
    }
    private static IndexKey Key(byte value) => new(new[] { value });
    private static RowId Row(ulong value) => new(new PageId(value), new SlotId(0), new SlotGeneration(0));
    private static LeafIndexEntry Entry(byte key, ulong row) => new(Key(key), Row(row));
    private static async Task<List<LeafIndexEntry>> CollectAsync(IAsyncEnumerable<LeafIndexEntry> source)
    {
        List<LeafIndexEntry> result = [];
        await foreach (var entry in source) result.Add(entry);
        return result;
    }
}
