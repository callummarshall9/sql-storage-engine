// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Exposures.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class PersistentBPlusTreeReadTests
{
    [Test]
    public async Task HandBuiltMultiLevelTree_SupportsDuplicatesAndBoundedRanges()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var fixture = await BuildFixtureAsync(store);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 3, leaveOpen: true);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(fixture.Root));
        (await tree.FindAsync(indexKey:Key(2))).Should().Equal(Row(20), Row(21), Row(22));
        (await CollectAsync(tree.ScanAsync(indexRange:new IndexRange { LowerBound = Key(2), UpperBound = Key(4), IncludeLowerBound = false, IncludeUpperBound = true, Direction = ScanDirection.Ascending }))).Select(entry => entry.Key).Should().Equal(Key(3), Key(4));
        (await CollectAsync(tree.ScanAsync(indexRange:new IndexRange { LowerBound = Key(2), UpperBound = Key(4), IncludeLowerBound = true, IncludeUpperBound = false, Direction = ScanDirection.Descending }))).Select(entry => entry.Key).Should().Equal(Key(3), Key(2), Key(2), Key(2));
        pool.ReadPinnedPageCount().Should().Be(0);
    }

    [Test]
    public async Task EarlyScanDisposalAndCancellationReleasePins()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var fixture = await BuildFixtureAsync(store);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 2, leaveOpen: true);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(fixture.Root));
        await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry _ in tree.ScanAsync(indexRange:new IndexRange { LowerBound = Key(1), UpperBound = Key(9), IncludeLowerBound = true, IncludeUpperBound = true, Direction = ScanDirection.Ascending }))
            break;
        pool.ReadPinnedPageCount().Should().Be(0);
        await ((Func<Task>)(async () => await CollectAsync(tree.ScanAsync(indexRange:new IndexRange { LowerBound = Key(1), UpperBound = Key(9), IncludeLowerBound = true, IncludeUpperBound = true, Direction = ScanDirection.Ascending },new CancellationToken(true))))).Should().ThrowAsync<OperationCanceledException>();
        pool.ReadPinnedPageCount().Should().Be(0);
    }

    [Test]
    public async Task WrongPageTypesAndLeafCyclesAreDetected()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var wrong = await store.AllocateAsync(PageType.Heap);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 3, leaveOpen: true);
        var wrongTree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(wrong));
        await ((Func<Task>)(async () => await wrongTree.FindAsync(indexKey:Key(1)))).Should().ThrowAsync<StorageFormatException>();
        var leaf = await store.AllocateAsync(PageType.BPlusTreeLeaf);
        var bytes = new byte[store.GetPageSize()];
        sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(bytes,new LeafIndexPage { PageId = leaf, ParentPageId = null, PreviousPageId = null, NextPageId = null, Entries = new[] { Entry(1,1), Entry(2,2) } });
        await store.WriteAsync(leaf,bytes);
        using (var pin = await pool.GetPageAsync(leaf))
        {
            pin.ReadMemory().Span[sql_storage_engine.Models.Indexes.LeafIndexPageFormat.NextOffset] = 1;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(pin.ReadMemory().Span[(sql_storage_engine.Models.Indexes.LeafIndexPageFormat.NextOffset + 1)..],leaf.Value);
            PageChecksumExposure.WriteChecksum(pin.ReadMemory().Span,pool.ReadPageSize());
            pin.MarkDirty(new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 });
        }

        var cyclic = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(leaf));
        await ((Func<Task>)(async () => await CollectAsync(cyclic.ScanAsync(indexRange:new IndexRange { LowerBound = Key(1), UpperBound = Key(9), IncludeLowerBound = true, IncludeUpperBound = true, Direction = ScanDirection.Ascending })))).Should().ThrowAsync<StorageCorruptionException>();
    }

    private static async Task<(PageId Root, PageId[] Leaves)> BuildFixtureAsync(sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store)
    {
        var leaves = new[]
        {
            await store.AllocateAsync(PageType.BPlusTreeLeaf),
            await store.AllocateAsync(PageType.BPlusTreeLeaf),
            await store.AllocateAsync(PageType.BPlusTreeLeaf),
            await store.AllocateAsync(PageType.BPlusTreeLeaf)
        };
        var leftInternal = await store.AllocateAsync(PageType.BPlusTreeInternal);
        var rightInternal = await store.AllocateAsync(PageType.BPlusTreeInternal);
        var root = await store.AllocateAsync(PageType.BPlusTreeInternal);
        var entries = new[]
        {
            new[]
            {
                Entry(1,10),
                Entry(2,20),
                Entry(2,21)
            },
            new[]
            {
                Entry(2,22),
                Entry(3,30)
            },
            new[]
            {
                Entry(4,40),
                Entry(5,50)
            },
            new[]
            {
                Entry(6,60),
                Entry(7,70)
            }
        };
        for (var index = 0; index < leaves.Length; index++)
        {
            var page = new byte[store.GetPageSize()];
            sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(page,new LeafIndexPage { PageId = leaves[index], ParentPageId = index < 2 ? leftInternal : rightInternal, PreviousPageId = index == 0 ? null : leaves[index - 1], NextPageId = index == leaves.Length - 1 ? null : leaves[index + 1], Entries = entries[index] });
            await store.WriteAsync(leaves[index],page);
        }

        await WriteInternal(store,leftInternal,root,new[] { Key(2) },leaves[..2]);
        await WriteInternal(store,rightInternal,root,new[] { Key(6) },leaves[2..]);
        await WriteInternal(store,root,null,new[] { Key(4) },new[] { leftInternal, rightInternal });
        return (root, leaves);
    }

    private static async Task WriteInternal(sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store, PageId id, PageId? parent, sql_storage_engine.Models.Indexes.IndexKey[] keys, PageId[] children)
    {
        var page = new byte[store.GetPageSize()];
        sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.WriteInternalIndexPage(page,new InternalIndexPage { PageId = id, ParentPageId = parent, Separators = keys, Children = children });
        await store.WriteAsync(id,page);
    }

    private static sql_storage_engine.Models.Indexes.IndexKey Key(byte value) => sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: new[] { value });
    private static RowId Row(ulong value) => new sql_storage_engine.Models.Identifiers.RowId
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
    private static sql_storage_engine.Models.Indexes.LeafIndexEntry Entry(byte key, ulong row) => sql_storage_engine.Exposures.Indexes.LeafIndexEntryExposure.CreateLeafIndexEntry(indexKey: Key(key),rowId: Row(row));
    private static async Task<List<sql_storage_engine.Models.Indexes.LeafIndexEntry>> CollectAsync(IAsyncEnumerable<sql_storage_engine.Models.Indexes.LeafIndexEntry> source)
    {
        List<sql_storage_engine.Models.Indexes.LeafIndexEntry> result = [];
        await foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in source)
            result.Add(entry);
        return result;
    }
}