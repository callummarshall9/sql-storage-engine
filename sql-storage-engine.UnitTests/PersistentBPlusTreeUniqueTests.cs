using AwesomeAssertions;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class PersistentBPlusTreeUniqueTests
{
    [Test]
    public async Task UniqueIndex_DuplicateKeyThrowsSpecificErrorAndPreservesExistingEntry()
    {
        await using var store = new InMemoryPageStore();
        var root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store,
            new[] { PersistentBPlusTreeInsertTests.Entry(7, 70) });
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var tree = new PersistentBPlusTree(pool, store, new MutableIndexRootReference(root), isUnique: true);

        var action = async () => await tree.InsertAsync(PersistentBPlusTreeInsertTests.Key(7),
            PersistentBPlusTreeInsertTests.Row(71));

        await action.Should().ThrowExactlyAsync<DuplicateIndexKeyException>();
        tree.IsUnique.Should().BeTrue();
        (await tree.FindAsync(PersistentBPlusTreeInsertTests.Key(7))).Should()
            .Equal(PersistentBPlusTreeInsertTests.Row(70));
    }

    [Test]
    public async Task NonUniqueIndex_DuplicateKeysRemainSupported()
    {
        await using var store = new InMemoryPageStore();
        var root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(store, []);
        await using var pool = new BufferPool(store, 2, leaveOpen: true);
        var tree = new PersistentBPlusTree(pool, store, new MutableIndexRootReference(root));

        await tree.InsertAsync(PersistentBPlusTreeInsertTests.Key(4), PersistentBPlusTreeInsertTests.Row(40));
        await tree.InsertAsync(PersistentBPlusTreeInsertTests.Key(4), PersistentBPlusTreeInsertTests.Row(41));

        tree.IsUnique.Should().BeFalse();
        (await tree.FindAsync(PersistentBPlusTreeInsertTests.Key(4))).Should().Equal(
            PersistentBPlusTreeInsertTests.Row(40), PersistentBPlusTreeInsertTests.Row(41));
    }

    [Test]
    public async Task UniqueIndex_CanonicalNullEncodingIsUniqueAcrossReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sql-index-unique-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "database.sse");
        var nullKey = new IndexKey(new byte[] { 0 });
        PageId root;
        try
        {
            await using (var database = await PageDatabase.CreateAsync(path))
            {
                root = await PersistentBPlusTreeInsertTests.WriteLeafAsync(database, []);
                await using var pool = new BufferPool(database, 2, leaveOpen: true);
                var tree = new PersistentBPlusTree(pool, database, new MutableIndexRootReference(root), isUnique: true);
                await tree.InsertAsync(nullKey, PersistentBPlusTreeInsertTests.Row(1));
                await pool.FlushAllAsync();
            }
            await using var reopened = await PageDatabase.OpenAsync(path);
            await using var reopenedPool = new BufferPool(reopened, 2, leaveOpen: true);
            var reopenedTree = new PersistentBPlusTree(reopenedPool, reopened, new MutableIndexRootReference(root), isUnique: true);
            var action = async () => await reopenedTree.InsertAsync(nullKey, PersistentBPlusTreeInsertTests.Row(2));
            await action.Should().ThrowExactlyAsync<DuplicateIndexKeyException>();
            (await reopenedTree.FindAsync(nullKey)).Should().Equal(PersistentBPlusTreeInsertTests.Row(1));
        }
        finally { Directory.Delete(directory, true); }
    }
}
