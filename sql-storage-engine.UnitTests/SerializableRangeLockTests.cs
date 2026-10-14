// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Transactions;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Exposures.LockResources;

using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.UnitTests;
public sealed class SerializableRangeLockTests
{
    private static readonly IndexId Index = new sql_storage_engine.Models.Identifiers.IndexId
    {
        Value = 9
    };
    [Test]
    public async Task OverlappingIncompatibleRanges_BlockWhileNonOverlappingRangesProceed()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        var protectedRange = Range(1,3,true,true);
        await manager.AcquireAsync(Tx(1),protectedRange,LockMode.Shared);
        var overlapping = manager.AcquireAsync(Tx(2),Range(3,5,true,true),LockMode.Exclusive).AsTask();
        var nonOverlapping = manager.AcquireAsync(Tx(3),Range(3,5,false,true),LockMode.Exclusive).AsTask();
        overlapping.IsCompleted.Should().BeFalse();
        nonOverlapping.IsCompleted.Should().BeFalse("FIFO preserves the earlier overlapping waiter");
        manager.Release(Tx(1),protectedRange);
        await overlapping;
        manager.Release(Tx(2),Range(3,5,true,true));
        await nonOverlapping;
    }

    [Test]
    public async Task NonOverlappingRanges_ProceedConcurrentlyWithoutEarlierConflict()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        await manager.AcquireAsync(Tx(1),Range(1,3,false,false),LockMode.Exclusive);
        var adjacent = manager.AcquireAsync(Tx(2),Range(3,5,true,true),LockMode.Exclusive).AsTask();
        await adjacent;
    }

    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    public void EndpointInclusion_MatchesBTreeRange(bool leftIncludesUpper, bool rightIncludesLower, bool overlaps)
    {
        sql_storage_engine.Exposures.LockResources.LockResourceExposure.CheckLockResourceConflict([Range(1,3,true,leftIncludesUpper), Range(3,5,rightIncludesLower,true)]).Should().Be(overlaps);
        var scan = new IndexRange
        {
            LowerBound = Key(1),
            UpperBound = Key(3),
            IncludeLowerBound = true,
            IncludeUpperBound = leftIncludesUpper,
            Direction = ScanDirection.Ascending
        };
        sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexRangeLockResourceFromRange(Index,scan).IncludeUpperBound.Should().Be(leftIncludesUpper);
    }

    [Test]
    public async Task InsertIntentInsideProtectedRange_WaitsUntilRangeRelease()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        var range = Range(1,5,true,true);
        await manager.AcquireAsync(Tx(1),range,LockMode.Shared);
        var insert = manager.AcquireAsync(Tx(2),sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexKeyLockResource(new sql_storage_engine.Models.LockResources.IndexKeyLockResource { IndexId = Index, Key = Key(3) }),LockMode.Exclusive).AsTask();
        insert.IsCompleted.Should().BeFalse();
        manager.Release(Tx(1),range);
        await insert;
    }

    [Test]
    public async Task EmptyRangesConflictWithNothingAndUnboundedRangesContainEveryKey()
    {
        var empty = Range(3,3,true,false);
        var unbounded = sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexRangeLockResource(new sql_storage_engine.Models.LockResources.IndexRangeLockResource { IncludeLowerBound = true, IncludeUpperBound = true, IndexId = Index, LowerBound = null, UpperBound = null });
        sql_storage_engine.Exposures.LockResources.LockResourceExposure.ReadIndexRangeLockResourceEmpty(empty).Should().BeTrue();
        sql_storage_engine.Exposures.LockResources.LockResourceExposure.CheckLockResourceConflict([empty, unbounded]).Should().BeFalse();
        sql_storage_engine.Exposures.LockResources.LockResourceExposure.CheckLockResourceConflict([unbounded, sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexKeyLockResource(new sql_storage_engine.Models.LockResources.IndexKeyLockResource { IndexId = Index, Key = Key(0) })]).Should().BeTrue();
        sql_storage_engine.Exposures.LockResources.LockResourceExposure.CheckLockResourceConflict([unbounded, sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexKeyLockResource(new sql_storage_engine.Models.LockResources.IndexKeyLockResource { IndexId = Index, Key = Key(255) })]).Should().BeTrue();
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        await manager.AcquireAsync(Tx(1),empty,LockMode.Exclusive);
        await manager.AcquireAsync(Tx(2),sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexKeyLockResource(new sql_storage_engine.Models.LockResources.IndexKeyLockResource { IndexId = Index, Key = Key(3) }),LockMode.Exclusive);
    }

    private static IndexRangeLockResource Range(byte lower, byte upper, bool includeLower, bool includeUpper) => sql_storage_engine.Exposures.LockResources.LockResourceExposure.CreateIndexRangeLockResource(new sql_storage_engine.Models.LockResources.IndexRangeLockResource { IncludeLowerBound = includeLower, IncludeUpperBound = includeUpper, IndexId = Index, LowerBound = Key(lower), UpperBound = Key(upper) });
    private static sql_storage_engine.Models.Indexes.IndexKey Key(byte value) => sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: [value]);
    private static TransactionId Tx(ulong value) => new sql_storage_engine.Models.Identifiers.TransactionId
    {
        Value = value
    };
}