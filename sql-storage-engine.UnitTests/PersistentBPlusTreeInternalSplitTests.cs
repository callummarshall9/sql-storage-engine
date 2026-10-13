// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.UnitTests;
public sealed class PersistentBPlusTreeInternalSplitTests
{
    [Test]
    public async Task RandomLargeKeyInserts_GrowBalancedTreeAndSurviveBufferReopen()
    {
        const int seed = 4411;
        var random = new Random(seed);
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, Array.Empty<sql_storage_engine.Models.Indexes.LeafIndexEntry>());
        sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure rootReference = new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root);
        var expected = Enumerable.Range(1, 100).OrderBy(_ => random.Next()).ToArray();
        await using (sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 8, leaveOpen: true))
        {
            var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,rootReference);
            foreach (var value in expected)
                await tree.InsertAsync(newIndexKey:LargeKey(value),newRowId:PersistentBPlusTreeInsertTests.Row((ulong)value + 1));
            await pool.FlushAllAsync();
            (await GetLeafDepthsAsync(pool,rootReference.ReadRootPageId())).Distinct().Should().ContainSingle();
            await AssertParentPointersAsync(pool,rootReference.ReadRootPageId(),null);
        }

        await using (sql_storage_engine.Exposures.Buffers.BufferPoolExposure reopenedPool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 8, leaveOpen: true))
        {
            var reopened = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(reopenedPool,store,rootReference);
            var scanned = await PersistentBPlusTreeInsertTests.CollectAsync(reopened.ScanAsync(indexRange:new IndexRange { LowerBound = LargeKey(1), UpperBound = LargeKey(100), IncludeLowerBound = true, IncludeUpperBound = true, Direction = ScanDirection.Ascending }));
            scanned.Select(entry => BinaryPrimitives.ReadInt32BigEndian(sql_storage_engine.Exposures.Indexes.IndexKeyExposure.ReadIndexKeyBytes(indexKey: entry.Key).Span[..4])).Should().Equal(Enumerable.Range(1, 100));
        }
    }

    private static sql_storage_engine.Models.Indexes.IndexKey LargeKey(int value)
    {
        var bytes = new byte[900];
        BinaryPrimitives.WriteInt32BigEndian(bytes,value);
        bytes.AsSpan(4).Fill(checked((byte)value));
        return sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: bytes);
    }

    private static async Task<List<int>> GetLeafDepthsAsync(sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool, PageId pageId, int depth = 0)
    {
        using var pin = await pool.GetPageAsync(pageId);
        var type = sql_storage_engine.Exposures.Pages.PageHeaderExposure.ReadPageHeader(pin.ReadMemory().Span).PageType;
        if (type == PageType.BPlusTreeLeaf)
            return new List<int>
            {
                depth
            };
        var node = sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.ReadInternalIndexPage(pin.ReadMemory().Span,(pageId).Value);
        var children = node.Children.ToArray();
        pin.Dispose();
        List<int> result = [];
        foreach (var child in children)
            result.AddRange(await GetLeafDepthsAsync(pool, child, depth + 1));
        return result;
    }

    private static async Task AssertParentPointersAsync(sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool, PageId pageId, PageId? expectedParent)
    {
        using var pin = await pool.GetPageAsync(pageId);
        var type = sql_storage_engine.Exposures.Pages.PageHeaderExposure.ReadPageHeader(pin.ReadMemory().Span).PageType;
        if (type == PageType.BPlusTreeLeaf)
        {
            sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(pin.ReadMemory().Span,(pageId).Value).ParentPageId.Should().Be(expectedParent);
            return;
        }

        var node = sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.ReadInternalIndexPage(pin.ReadMemory().Span,(pageId).Value);
        node.ParentPageId.Should().Be(expectedParent);
        var children = node.Children.ToArray();
        pin.Dispose();
        foreach (var child in children)
            await AssertParentPointersAsync(pool, child, pageId);
    }
}