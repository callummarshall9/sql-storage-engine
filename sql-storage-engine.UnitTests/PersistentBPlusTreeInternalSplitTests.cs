using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;

namespace sql_storage_engine.UnitTests;

public sealed class PersistentBPlusTreeInternalSplitTests
{
    [Test]
    public async Task RandomLargeKeyInserts_GrowBalancedTreeAndSurviveBufferReopen()
    {
        const int seed = 4411;
        var random = new Random(seed);
        await using var store = new InMemoryPageStore();
        var root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, Array.Empty<LeafIndexEntry>());
        var rootReference = new MutableIndexRootReference(root);
        var expected = Enumerable.Range(1, 100).OrderBy(_ => random.Next()).ToArray();
        await using (var pool = new BufferPool(store, 8, leaveOpen: true))
        {
            var tree = new PersistentBPlusTree(pool, store, rootReference);
            foreach (var value in expected)
                await tree.InsertAsync(LargeKey(value), PersistentBPlusTreeInsertTests.Row((ulong)value + 1));
            await pool.FlushAllAsync();
            (await GetLeafDepthsAsync(pool, rootReference.RootPageId)).Distinct().Should().ContainSingle();
            await AssertParentPointersAsync(pool, rootReference.RootPageId, null);
        }

        await using (var reopenedPool = new BufferPool(store, 8, leaveOpen: true))
        {
            var reopened = new PersistentBPlusTree(reopenedPool, store, rootReference);
            var scanned = await PersistentBPlusTreeInsertTests.CollectAsync(
                reopened.ScanAsync(new IndexRange(LargeKey(1), LargeKey(100))));
            scanned.Select(entry => BinaryPrimitives.ReadInt32BigEndian(entry.Key.Bytes.Span[..4]))
                .Should().Equal(Enumerable.Range(1, 100));
        }
    }

    private static IndexKey LargeKey(int value)
    {
        var bytes = new byte[900];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        bytes.AsSpan(4).Fill(checked((byte)value));
        return new IndexKey(bytes);
    }

    private static async Task<List<int>> GetLeafDepthsAsync(BufferPool pool, PageId pageId, int depth = 0)
    {
        using var pin = await pool.GetPageAsync(pageId);
        var type = PageHeaderCodec.Read(pin.Memory.Span).PageType;
        if (type == PageType.BPlusTreeLeaf) return new List<int> { depth };
        var node = InternalIndexPageCodec.Read(pin.Memory.Span, pageId);
        var children = node.Children.ToArray();
        pin.Dispose();
        List<int> result = [];
        foreach (var child in children) result.AddRange(await GetLeafDepthsAsync(pool, child, depth + 1));
        return result;
    }

    private static async Task AssertParentPointersAsync(BufferPool pool, PageId pageId, PageId? expectedParent)
    {
        using var pin = await pool.GetPageAsync(pageId);
        var type = PageHeaderCodec.Read(pin.Memory.Span).PageType;
        if (type == PageType.BPlusTreeLeaf)
        {
            LeafIndexPageCodec.Read(pin.Memory.Span, pageId).ParentPageId.Should().Be(expectedParent);
            return;
        }
        var node = InternalIndexPageCodec.Read(pin.Memory.Span, pageId);
        node.ParentPageId.Should().Be(expectedParent);
        var children = node.Children.ToArray();
        pin.Dispose();
        foreach (var child in children) await AssertParentPointersAsync(pool, child, pageId);
    }
}
