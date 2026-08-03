using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Rows;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class SerializableRangeLockTests
{
    private static readonly IndexId Index = new(9);

    [Test]
    public async Task OverlappingIncompatibleRanges_BlockWhileNonOverlappingRangesProceed()
    {
        var manager = new LockManager();
        var protectedRange = Range(1, 3, true, true);
        await manager.AcquireAsync(Tx(1), protectedRange, LockMode.Shared);

        var overlapping = manager.AcquireAsync(Tx(2), Range(3, 5, true, true), LockMode.Exclusive).AsTask();
        var nonOverlapping = manager.AcquireAsync(Tx(3), Range(3, 5, false, true), LockMode.Exclusive).AsTask();

        overlapping.IsCompleted.Should().BeFalse();
        nonOverlapping.IsCompleted.Should().BeFalse("FIFO preserves the earlier overlapping waiter");
        manager.Release(Tx(1), protectedRange);
        await overlapping;
        manager.Release(Tx(2), Range(3, 5, true, true));
        await nonOverlapping;
    }

    [Test]
    public async Task NonOverlappingRanges_ProceedConcurrentlyWithoutEarlierConflict()
    {
        var manager = new LockManager();
        await manager.AcquireAsync(Tx(1), Range(1, 3, false, false), LockMode.Exclusive);
        var adjacent = manager.AcquireAsync(Tx(2), Range(3, 5, true, true), LockMode.Exclusive).AsTask();
        await adjacent;
    }

    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    public void EndpointInclusion_MatchesBTreeRange(bool leftIncludesUpper, bool rightIncludesLower, bool overlaps)
    {
        LockResourceRelations.Overlap(Range(1, 3, true, leftIncludesUpper),
            Range(3, 5, rightIncludesLower, true)).Should().Be(overlaps);
        var scan = new IndexRange(Key(1), Key(3), true, leftIncludesUpper);
        IndexRangeLockResource.From(Index, scan).IncludeUpperBound.Should().Be(leftIncludesUpper);
    }

    [Test]
    public async Task InsertIntentInsideProtectedRange_WaitsUntilRangeRelease()
    {
        var manager = new LockManager();
        var range = Range(1, 5, true, true);
        await manager.AcquireAsync(Tx(1), range, LockMode.Shared);
        var insert = manager.AcquireAsync(Tx(2), new IndexKeyLockResource(Index, Key(3)), LockMode.Exclusive).AsTask();
        insert.IsCompleted.Should().BeFalse();
        manager.Release(Tx(1), range);
        await insert;
    }

    [Test]
    public async Task SerializableRepeatedScan_PreventsPhantomInsert()
    {
        await using var fixture = await TableStorageInsertTests.Fixture.CreateAsync();
        var existing = new Row([SqlValue.Integer(2), SqlValue.Text("two")]);
        await fixture.Table.InsertAsync(existing);
        var index = fixture.Indexes[0];
        var lower = CatalogIndexKey.Encode(new Row([SqlValue.Integer(1), SqlValue.Text("x")]),
            fixture.Definition, index.Definition);
        var upper = CatalogIndexKey.Encode(new Row([SqlValue.Integer(5), SqlValue.Text("x")]),
            fixture.Definition, index.Definition);
        var insertedKey = CatalogIndexKey.Encode(new Row([SqlValue.Integer(3), SqlValue.Text("three")]),
            fixture.Definition, index.Definition);
        var manager = new LockManager();
        var transactions = new TransactionManager();
        var scannerTransaction = new LockingTransaction(transactions.Begin(), manager);
        var inserterTransaction = new LockingTransaction(transactions.Begin(), manager);
        var scanner = new TransactionalIndex(index.Tree, index.Definition.Id, scannerTransaction,
            TransactionIsolationLevel.Serializable);
        var inserter = new TransactionalIndex(index.Tree, index.Definition.Id, inserterTransaction,
            TransactionIsolationLevel.Serializable);
        var range = new IndexRange(lower, upper);

        (await scanner.ScanAsync(range)).Should().HaveCount(1);
        var insert = inserter.InsertAsync(insertedKey,
            new RowId(new PageId(999), new SlotId(1), new SlotGeneration(1))).AsTask();
        insert.IsCompleted.Should().BeFalse();
        (await scanner.ScanAsync(range)).Should().HaveCount(1);
        scannerTransaction.Commit();
        await insert;
        inserterTransaction.Commit();
        var entries = new List<LeafIndexEntry>();
        await foreach (var entry in index.Tree.ScanAsync(range)) entries.Add(entry);
        entries.Should().HaveCount(2);
    }

    [Test]
    public async Task EmptyRangesConflictWithNothingAndUnboundedRangesContainEveryKey()
    {
        var empty = Range(3, 3, true, false);
        var unbounded = new IndexRangeLockResource(Index, null, null);
        empty.IsEmpty.Should().BeTrue();
        LockResourceRelations.Overlap(empty, unbounded).Should().BeFalse();
        LockResourceRelations.Contains(unbounded, new IndexKeyLockResource(Index, Key(0))).Should().BeTrue();
        LockResourceRelations.Contains(unbounded, new IndexKeyLockResource(Index, Key(255))).Should().BeTrue();

        var manager = new LockManager();
        await manager.AcquireAsync(Tx(1), empty, LockMode.Exclusive);
        await manager.AcquireAsync(Tx(2), new IndexKeyLockResource(Index, Key(3)), LockMode.Exclusive);
    }

    private static IndexRangeLockResource Range(byte lower, byte upper, bool includeLower, bool includeUpper) =>
        new(Index, Key(lower), Key(upper), includeLower, includeUpper);
    private static IndexKey Key(byte value) => new([value]);
    private static TransactionId Tx(ulong value) => new(value);
}
