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
public sealed class PersistentBPlusTreeLeafSplitTests
{
    [Test]
    public async Task RootLeafSplit_CreatesBalancedLinkedLeavesAndValidInternalRoot()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var entries = FillLeaf(store.GetPageSize());
        var oldRoot = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, entries);
        sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure rootReference = new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(oldRoot);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 4, leaveOpen: true);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,rootReference);
        sql_storage_engine.Models.Indexes.LeafIndexEntry inserted = sql_storage_engine.Exposures.Indexes.LeafIndexEntryExposure.CreateLeafIndexEntry(indexKey: IntEntryKey(entries.Count + 1),rowId: PersistentBPlusTreeInsertTests.Row(9999));
        await tree.InsertAsync(newIndexKey:inserted.Key,newRowId:inserted.RowId);
        rootReference.ReadRootPageId().Should().NotBe(oldRoot);
        InternalIndexPage root;
        using (var rootPin = await pool.GetPageAsync(rootReference.ReadRootPageId()))
            root = sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.ReadInternalIndexPage(rootPin.ReadMemory().Span,(rootReference.ReadRootPageId()).Value);
        root.Children.Should().HaveCount(2);
        LeafIndexPage left;
        LeafIndexPage right;
        using (var pin = await pool.GetPageAsync(root.Children[0]))
            left = sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(pin.ReadMemory().Span,(root.Children[0]).Value);
        using (var pin = await pool.GetPageAsync(root.Children[1]))
            right = sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(pin.ReadMemory().Span,(root.Children[1]).Value);
        left.Entries.Count.Should().BeGreaterThanOrEqualTo((entries.Count + 1) / 2);
        right.Entries.Count.Should().BeGreaterThanOrEqualTo((entries.Count + 1) / 2);
        left.NextPageId.Should().Be(right.PageId);
        right.PreviousPageId.Should().Be(left.PageId);
        left.ParentPageId.Should().Be(root.PageId);
        right.ParentPageId.Should().Be(root.PageId);
        left.Entries.Concat(right.Entries).Should().HaveCount(entries.Count + 1).And.ContainSingle(entry => entry.RowId == inserted.RowId);
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
            sql_storage_engine.Models.Indexes.IndexKey insertedKey;
            await using (var database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                var entries = FillLeaf(database.GetPageSize());
                var oldRoot = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database, entries);
                sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure reference = new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(oldRoot);
                await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 4, leaveOpen: true);
                var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,database,reference);
                insertedKey = IntEntryKey(entries.Count + 1);
                await tree.InsertAsync(newIndexKey:insertedKey,newRowId:PersistentBPlusTreeInsertTests.Row(9999));
                root = reference.ReadRootPageId();
                await pool.FlushAllAsync();
            }

            await using (var database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path))
            await using (sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 4, leaveOpen: true))
            {
                var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,database,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root));
                (await tree.FindAsync(indexKey:insertedKey)).Should().Equal(PersistentBPlusTreeInsertTests.Row(9999));
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static List<sql_storage_engine.Models.Indexes.LeafIndexEntry> FillLeaf(int pageSize)
    {
        List<sql_storage_engine.Models.Indexes.LeafIndexEntry> entries = [];
        for (var value = 1;; value++)
        {
            sql_storage_engine.Models.Indexes.LeafIndexEntry entry = sql_storage_engine.Exposures.Indexes.LeafIndexEntryExposure.CreateLeafIndexEntry(indexKey: IntEntryKey(value),rowId: PersistentBPlusTreeInsertTests.Row((ulong)value + 1));
            if (!sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.CheckLeafIndexPageCapacity(pageSize, entries.Append(entry)))
                return entries;
            entries.Add(entry);
        }
    }

    private static sql_storage_engine.Models.Indexes.IndexKey IntEntryKey(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes,value);
        return sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: bytes);
    }
}