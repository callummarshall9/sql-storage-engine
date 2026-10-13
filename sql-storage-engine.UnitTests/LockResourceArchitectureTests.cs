using AwesomeAssertions;
using sql_storage_engine.Exposures.Indexes;
using sql_storage_engine.Exposures.LockResources;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Transactions;

namespace sql_storage_engine.UnitTests;

public sealed class LockResourceArchitectureTests
{
    private static IndexKey Key(byte value) => IndexKeyExposure.CreateIndexKey([value]);

    [Test]
    public void RawInvalidRangesAndKeysAreRejectedBeforeLockAdmission()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        var transactionId = new TransactionId { Value = 1 };
        var range = new IndexRangeLockResource { IndexId = new IndexId { Value = 1 }, LowerBound = Key(2), UpperBound = Key(1) };
        ((Func<ValueTask>)(() => manager.AcquireAsync(transactionId,range,LockMode.Exclusive))).Should().Throw<ArgumentException>();
        var key = new IndexKeyLockResource { IndexId = new IndexId { Value = 1 } };
        ((Func<ValueTask>)(() => manager.AcquireAsync(transactionId,key,LockMode.Exclusive))).Should().Throw<ArgumentException>();
    }

    [Test]
    public async Task UnboundedFlagsAreNormalizedForAcquisitionAndRelease()
    {
        var manager = new sql_storage_engine.Exposures.Transactions.LockManagerExposure();
        var transactionId = new TransactionId { Value = 1 };
        var raw = new IndexRangeLockResource { IndexId = new IndexId { Value = 1 }, IncludeLowerBound = true, IncludeUpperBound = true };
        await manager.AcquireAsync(transactionId,raw,LockMode.Exclusive);
        var normalized = LockResourceExposure.CreateIndexRangeLockResource(raw);
        normalized.IncludeLowerBound.Should().BeFalse();
        normalized.IncludeUpperBound.Should().BeFalse();
        manager.Release(transactionId,normalized).Should().BeTrue();
        manager.Release(transactionId,raw).Should().BeFalse();
    }

    [Test]
    public void ConflictDomainsAreSymmetricAndPreserveValueEquality()
    {
        var indexId = new IndexId { Value = 1 };
        var range = LockResourceExposure.CreateIndexRangeLockResource(new IndexRangeLockResource { IndexId = indexId, LowerBound = Key(1), UpperBound = Key(2), IncludeLowerBound = true, IncludeUpperBound = true });
        var key = LockResourceExposure.CreateIndexKeyLockResource(new IndexKeyLockResource { IndexId = indexId, Key = Key(2) });
        LockResourceExposure.CheckLockResourceConflict([range, key]).Should().BeTrue();
        LockResourceExposure.CheckLockResourceConflict([key, range]).Should().BeTrue();
        var otherIndex = key with { IndexId = new IndexId { Value = 2 } };
        LockResourceExposure.CheckLockResourceConflict([range, otherIndex]).Should().BeFalse();
        new HashSet<LockResource> { key }.Contains(new IndexKeyLockResource { IndexId = indexId, Key = Key(2) }).Should().BeTrue();
        ((Action)(() => LockResourceExposure.CheckLockResourceConflict([key]))).Should().Throw<ArgumentException>();
    }
}