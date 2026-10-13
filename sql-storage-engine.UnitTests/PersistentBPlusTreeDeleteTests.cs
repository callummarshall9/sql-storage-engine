// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.UnitTests;
public sealed class PersistentBPlusTreeDeleteTests
{
    [Test]
    public async Task Delete_WithCancellation_DoesNotMutateRootLeaf()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, new[] { Entry(1,10), Entry(2,20) });
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 2, leaveOpen: true);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = async () => await tree.DeleteAsync(deletedIndexKey:Key(1),deletedRowId:Row(10),cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
        (await tree.FindAsync(indexKey:Key(1))).Should().Equal(Row(10));
    }

    [Test]
    public async Task LeafMerge_ContractsRootMaintainsLinksAndReportsRetiredPages()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var left = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, new[] { Entry(1,10), Entry(2,20) });
        var right = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, new[] { Entry(3,30), Entry(4,40) });
        var root = await store.AllocateAsync(PageType.BPlusTreeInternal);
        await RewriteLeaf(store, left, root, null, right);
        await RewriteLeaf(store, right, root, left, null);
        await PersistentBPlusTreeInsertTests.WriteInternalAsync(store, root, new[] { Key(3) }, new[] { left, right });
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 4, leaveOpen: true);
        sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure rootReference = new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,rootReference);
        var result = await tree.DeleteAsync(deletedIndexKey:Key(1),deletedRowId:Row(10));
        result.Removed.Should().BeTrue();
        rootReference.ReadRootPageId().Should().Be(left);
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
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, new[] { Entry(1,10), Entry(2,20), Entry(2,21), Entry(3,30) });
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 2, leaveOpen: true);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root));
        (await tree.RemoveAsync(indexKey:Key(2),rowId:Row(20))).Should().BeTrue();
        (await tree.FindAsync(indexKey:Key(2))).Should().Equal(Row(21));
        using var beforePin = await pool.GetPageAsync(root);
        var before = beforePin.ReadMemory().ToArray();
        beforePin.Dispose();
        (await tree.RemoveAsync(indexKey:Key(2),rowId:Row(999))).Should().BeFalse();
        using var afterPin = await pool.GetPageAsync(root);
        afterPin.ReadMemory().ToArray().Should().Equal(before);
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
            await using (var database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                var left = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database, borrowLeft ? new[] { Entry(1,10), Entry(2,20), Entry(3,30) } : new[] { Entry(1,10), Entry(2,20) });
                middle = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database, new[] { Entry(4,40), Entry(5,50) });
                var right = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database, borrowLeft ? new[] { Entry(6,60), Entry(7,70) } : new[] { Entry(6,60), Entry(7,70), Entry(8,80) });
                root = await database.AllocateAsync(PageType.BPlusTreeInternal);
                await RewriteLeaf(database, left, root, null, middle);
                await RewriteLeaf(database, middle, root, left, right);
                await RewriteLeaf(database, right, root, middle, null);
                await PersistentBPlusTreeInsertTests.WriteInternalAsync(database, root, new[] { Key(4), Key(6) }, new[] { left, middle, right });
                await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 4, leaveOpen: true);
                var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,database,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root));
                (await tree.RemoveAsync(indexKey:Key(4),rowId:Row(40))).Should().BeTrue();
                await pool.FlushAllAsync();
            }

            await using (var database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path))
            await using (sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 4, leaveOpen: true))
            {
                LeafIndexPage leaf;
                using (var pin = await pool.GetPageAsync(middle))
                    leaf = sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(pin.ReadMemory().Span,(middle).Value);
                leaf.Entries.Should().HaveCount(2);
                InternalIndexPage node;
                using (var pin = await pool.GetPageAsync(root))
                    node = sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.ReadInternalIndexPage(pin.ReadMemory().Span,(root).Value);
                node.Separators[0].Should().Be(leaf.Entries[0].Key);
                node.Separators[1].Should().Be((await ReadLeaf(pool,node.Children[2])).Entries[0].Key);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
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
            await using (var database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                var initialRoot = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database, []);
                sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure rootReference = new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(initialRoot);
                await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 8, leaveOpen: true);
                var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,database,rootReference);
                foreach (var pair in expected)
                    await tree.InsertAsync(newIndexKey:LargeKey(pair.Key),newRowId:pair.Value);
                foreach (var value in deletionOrder)
                {
                    var result = await tree.DeleteAsync(deletedIndexKey:LargeKey(value),deletedRowId:expected[value]);
                    result.Removed.Should().BeTrue($"fixed seed {seed} must delete value {value}");
                    retired.AddRange(result.RetiredPageIds);
                    expected.Remove(value);
                }

                reopenedRoot = rootReference.ReadRootPageId();
                await pool.FlushAllAsync();
            }

            retired.Should().NotBeEmpty($"fixed seed {seed} must exercise merge retirement");
            await using var reopened = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path);
            await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure reopenedPool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(reopened, 8, leaveOpen: true);
            var reopenedTree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(reopenedPool,reopened,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(reopenedRoot));
            var actual = new List<sql_storage_engine.Models.Indexes.LeafIndexEntry>();
            await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in reopenedTree.ScanAsync(indexRange:new IndexRange { LowerBound = LargeKey(0), UpperBound = LargeKey(255), IncludeLowerBound = true, IncludeUpperBound = true, Direction = ScanDirection.Ascending }))
                actual.Add(entry);
            actual.Select(entry => entry.RowId).Should().Equal(expected.OrderBy(pair => pair.Key).Select(pair => pair.Value), $"fixed seed {seed} must survive close and reopen");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static async Task RewriteLeaf(sql_storage_engine.Exposures.Pages.IPageStoreExposure store, PageId id, PageId parent, PageId? previous, PageId? next)
    {
        var bytes = new byte[store.GetPageSize()];
        await store.ReadAsync(id,bytes);
        var leaf = sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(bytes,(id).Value);
        sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(bytes,leaf with { ParentPageId = parent, PreviousPageId = previous, NextPageId = next });
        await store.WriteAsync(id,bytes);
    }

    private static async Task<LeafIndexPage> ReadLeaf(sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool, PageId id)
    {
        using var pin = await pool.GetPageAsync(id);
        return sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(pin.ReadMemory().Span,(id).Value);
    }

    private static sql_storage_engine.Models.Indexes.IndexKey Key(byte value) => PersistentBPlusTreeInsertTests.Key(value);
    private static RowId Row(ulong value) => PersistentBPlusTreeInsertTests.Row(value);
    private static sql_storage_engine.Models.Indexes.LeafIndexEntry Entry(byte key, ulong row) => PersistentBPlusTreeInsertTests.Entry(key,row);
    private static sql_storage_engine.Models.Indexes.IndexKey LargeKey(int value)
    {
        var bytes = new byte[900];
        bytes[0] = (byte)(value >> 8);
        bytes[1] = (byte)value;
        return sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: bytes);
    }
}