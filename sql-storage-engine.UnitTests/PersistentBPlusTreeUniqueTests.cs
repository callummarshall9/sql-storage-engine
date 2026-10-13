using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.UnitTests;
public sealed class PersistentBPlusTreeUniqueTests
{
    [Test]
    public async Task UniqueIndex_DuplicateKeyThrowsSpecificErrorAndPreservesExistingEntry()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, new[] { PersistentBPlusTreeInsertTests.Entry(7,70) });
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 2, leaveOpen: true);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root),isUnique: true);
        var action = async () => await tree.InsertAsync(newIndexKey:PersistentBPlusTreeInsertTests.Key(7),newRowId:PersistentBPlusTreeInsertTests.Row(71));
        await action.Should().ThrowExactlyAsync<DuplicateIndexKeyException>();
        tree.ReadIsUnique().Should().BeTrue();
        (await tree.FindAsync(indexKey:PersistentBPlusTreeInsertTests.Key(7))).Should().Equal(PersistentBPlusTreeInsertTests.Row(70));
    }

    [Test]
    public async Task NonUniqueIndex_DuplicateKeysRemainSupported()
    {
        await using sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure store = new sql_storage_engine.Exposures.Pages.MemoryPageStoreExposure();
        var root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, []);
        await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(store, 2, leaveOpen: true);
        var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,store,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root));
        await tree.InsertAsync(newIndexKey:PersistentBPlusTreeInsertTests.Key(4),newRowId:PersistentBPlusTreeInsertTests.Row(40));
        await tree.InsertAsync(newIndexKey:PersistentBPlusTreeInsertTests.Key(4),newRowId:PersistentBPlusTreeInsertTests.Row(41));
        tree.ReadIsUnique().Should().BeFalse();
        (await tree.FindAsync(indexKey:PersistentBPlusTreeInsertTests.Key(4))).Should().Equal(PersistentBPlusTreeInsertTests.Row(40), PersistentBPlusTreeInsertTests.Row(41));
    }

    [Test]
    public async Task UniqueIndex_CanonicalNullEncodingIsUniqueAcrossReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sql-index-unique-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "database.sse");
        sql_storage_engine.Models.Indexes.IndexKey nullKey = sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: new byte[] { 0 });
        PageId root;
        try
        {
            await using (var database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database, []);
                await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure pool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(database, 2, leaveOpen: true);
                var tree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(pool,database,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root),isUnique: true);
                await tree.InsertAsync(newIndexKey:nullKey,newRowId:PersistentBPlusTreeInsertTests.Row(1));
                await pool.FlushAllAsync();
            }

            await using var reopened = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path);
            await using sql_storage_engine.Exposures.Buffers.BufferPoolExposure reopenedPool = new sql_storage_engine.Exposures.Buffers.BufferPoolExposure(reopened, 2, leaveOpen: true);
            var reopenedTree = new sql_storage_engine.Exposures.Indexes.PersistentBPlusTreeExposure(reopenedPool,reopened,new sql_storage_engine.Exposures.Indexes.IndexRootReferenceExposure(root),isUnique: true);
            var action = async () => await reopenedTree.InsertAsync(newIndexKey:nullKey,newRowId:PersistentBPlusTreeInsertTests.Row(2));
            await action.Should().ThrowExactlyAsync<DuplicateIndexKeyException>();
            (await reopenedTree.FindAsync(indexKey:nullKey)).Should().Equal(PersistentBPlusTreeInsertTests.Row(1));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}