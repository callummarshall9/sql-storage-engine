// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using System.Buffers.Binary;
using sql_storage_engine.Buffers;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.UnitTests;
public sealed class PersistentBPlusTreeInsertTests
{
    [Test]
    public async Task InsertWithoutSplit_PreservesOrderAndEveryDuplicateRowId()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var root = await WriteLeafAsync(store, Array.Empty<sql_storage_engine.Models.Indexes.LeafIndexEntry>());
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 2, leaveOpen: true);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root));
        (await tree.InsertWithoutSplitAsync(newIndexKey:Key(2),newRowId:Row(20))).Should().Be(IndexInsertResult.Inserted);
        (await tree.InsertWithoutSplitAsync(newIndexKey:Key(1),newRowId:Row(10))).Should().Be(IndexInsertResult.Inserted);
        (await tree.InsertWithoutSplitAsync(newIndexKey:Key(2),newRowId:Row(21))).Should().Be(IndexInsertResult.Inserted);
        (await tree.FindAsync(indexKey:Key(2))).Should().Equal(Row(20), Row(21));
        (await CollectAsync(tree.ScanAsync(indexRange:new IndexRange { LowerBound = Key(1), UpperBound = Key(2), IncludeLowerBound = true, IncludeUpperBound = true, Direction = ScanDirection.Ascending }))).Select(entry => entry.Key).Should().Equal(Key(1), Key(2), Key(2));
    }

    [Test]
    public async Task FullLeaf_ReturnsSplitRequiredWithoutPartialMutation()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var entries = new List<sql_storage_engine.Models.Indexes.LeafIndexEntry>();
        for (var value = 1;; value++)
        {
            var keyBytes = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(keyBytes,value);
            var candidate = entries.Append(sql_storage_engine.Exposures.Indexes.LeafIndexEntryExposure.CreateLeafIndexEntry(indexKey: sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: keyBytes),rowId: Row((ulong)value + 1))).ToArray();
            if (!sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.CheckLeafIndexPageCapacity(store.GetPageSize(), candidate))
                break;
            entries.Add(candidate[^1]);
        }

        var root = await WriteLeafAsync(store, entries);
        var before = new byte[store.GetPageSize()];
        await store.ReadAsync(root, before);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 1, leaveOpen: true);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root));
        (await tree.InsertWithoutSplitAsync(newIndexKey:sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: new byte[] { 0xff, 0xff, 0xff, 0xff }),newRowId:Row(9999))).Should().Be(IndexInsertResult.SplitRequired);
        using var pin = await pool.GetPageAsync(root);
        pin.ReadMemory().ToArray().Should().Equal(before);
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
            await using (var database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                var left = await WriteLeafAsync(database, new[] { Entry(1,10), Entry(2,20) });
                var right = await WriteLeafAsync(database, new[] { Entry(5,50), Entry(6,60) });
                root = await database.AllocateAsync(PageType.BPlusTreeInternal);
                await WriteInternalAsync(database, root, new[] { Key(5) }, new[] { left, right });
                await RewriteLeafParentAsync(database, left, root);
                await RewriteLeafParentAsync(database, right, root);
                await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 3, leaveOpen: true);
                var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,database,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root));
                (await tree.InsertWithoutSplitAsync(newIndexKey:Key(4),newRowId:Row(40))).Should().Be(IndexInsertResult.Inserted);
                await pool.FlushAllAsync();
            }

            await using (var database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path))
            await using (sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 3, leaveOpen: true))
            {
                var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,database,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root));
                (await tree.FindAsync(indexKey:Key(4))).Should().Equal(Row(40));
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    internal static sql_storage_engine.Models.Indexes.IndexKey Key(byte value) => sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: new[] { value });
    internal static RowId Row(ulong value) => new sql_storage_engine.Models.Identifiers.RowId
    {
        PageId = new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = value
        },
        SlotId = new sql_storage_engine.Models.Identifiers.SlotId
        {
            Value = 0
        },
        Generation = new sql_storage_engine.Models.Identifiers.SlotGeneration
        {
            Value = 0
        }
    };
    internal static sql_storage_engine.Models.Indexes.LeafIndexEntry Entry(byte key, ulong row) => sql_storage_engine.Exposures.Indexes.LeafIndexEntryExposure.CreateLeafIndexEntry(indexKey: Key(key),rowId: Row(row));
    internal static async Task<PageId> WriteLeafAsync(sql_storage_engine.Exposures.Pages.IPageStoreExposure store, IEnumerable<sql_storage_engine.Models.Indexes.LeafIndexEntry> entries, PageId? id = null, PageId? parent = null, PageId? previous = null, PageId? next = null)
    {
        if (store is not sql_storage_engine.Exposures.Pages.IPageAllocatorExposure allocator)
            throw new ArgumentException("Store must allocate pages.");
        var pageId = id ?? await allocator.AllocateAsync(PageType.BPlusTreeLeaf);
        var bytes = new byte[store.GetPageSize()];
        sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(bytes,new LeafIndexPage { PageId = pageId, ParentPageId = parent, PreviousPageId = previous, NextPageId = next, Entries = entries.ToArray() });
        await store.WriteAsync(pageId,bytes);
        return pageId;
    }

    internal static async Task WriteInternalAsync(sql_storage_engine.Exposures.Pages.IPageStoreExposure store, PageId id, sql_storage_engine.Models.Indexes.IndexKey[] separators, PageId[] children, PageId? parent = null)
    {
        var bytes = new byte[store.GetPageSize()];
        sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.WriteInternalIndexPage(bytes,new InternalIndexPage { PageId = id, ParentPageId = parent, Separators = separators, Children = children });
        await store.WriteAsync(id,bytes);
    }

    internal static async Task RewriteLeafParentAsync(sql_storage_engine.Exposures.Pages.IPageStoreExposure store, PageId id, PageId parent)
    {
        var bytes = new byte[store.GetPageSize()];
        await store.ReadAsync(id,bytes);
        var leaf = sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(bytes,(id).Value);
        sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(bytes,leaf with { ParentPageId = parent });
        await store.WriteAsync(id,bytes);
    }

    internal static async Task<List<sql_storage_engine.Models.Indexes.LeafIndexEntry>> CollectAsync(IAsyncEnumerable<sql_storage_engine.Models.Indexes.LeafIndexEntry> source)
    {
        List<sql_storage_engine.Models.Indexes.LeafIndexEntry> result = [];
        await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in source)
            result.Add(entry);
        return result;
    }
}