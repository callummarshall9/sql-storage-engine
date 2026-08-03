using AwesomeAssertions;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class StorageLimitsTests
{
    [Test]
    public void Defaults_ArePositiveAndWithinPublishedHardMaxima()
    {
        var limits = new StorageLimits();
        limits.BufferFrames.Should().BeInRange(1, StorageLimits.MaximumBufferFrames);
        limits.RowBytes.Should().BeInRange(1, StorageLimits.MaximumRowBytes);
        limits.KeyBytes.Should().BeInRange(1, StorageLimits.MaximumKeyBytes);
        limits.ValueBytes.Should().BeInRange(1, StorageLimits.MaximumValueBytes);
        limits.OverflowPages.Should().BeInRange(1, StorageLimits.MaximumOverflowPages);
        limits.TransactionDuration.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(StorageLimits.MaximumTransactionSeconds));
        limits.UndoBytes.Should().BeInRange(1, StorageLimits.MaximumUndoBytes);
        limits.PinsPerTransaction.Should().BeInRange(1, StorageLimits.MaximumPinsPerTransaction);
        limits.ScanPages.Should().BeInRange(1, StorageLimits.MaximumScanPages);
        limits.ConcurrentTransactions.Should().BeInRange(1, StorageLimits.MaximumConcurrentTransactions);
    }

    [Test]
    public void ValuesBeyondPublishedMaximum_AreRejected()
    {
        ((Func<StorageLimits>)(() => new StorageLimits(rowBytes: StorageLimits.MaximumRowBytes + 1)))
            .Should().Throw<ArgumentOutOfRangeException>();
        ((Func<StorageLimits>)(() => new StorageLimits(concurrentTransactions: 0)))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ExceededLimit_UsesStableResourceErrorBeforeAllocation()
    {
        var limiter = new StorageResourceLimiter(new StorageLimits(rowBytes: 4));
        var allocated = false;
        var action = () => { limiter.ValidateRowLength(5); allocated = true; _ = new byte[5]; };
        var exception = action.Should().Throw<StorageResourceExhaustedException>().Which;
        exception.Message.Should().StartWith("ROW_BYTES limit 4 exceeded");
        allocated.Should().BeFalse();
    }

    [Test]
    public void FailureAndDisposal_ReleaseHeldResources()
    {
        var limiter = new StorageResourceLimiter(new StorageLimits(concurrentTransactions: 1, pinsPerTransaction: 1));
        using (limiter.AcquireTransaction())
        using (limiter.AcquirePin())
        {
            ((Func<IDisposable>)limiter.AcquireTransaction).Should().Throw<StorageResourceExhaustedException>();
            ((Func<IDisposable>)limiter.AcquirePin).Should().Throw<StorageResourceExhaustedException>();
        }
        limiter.ActiveTransactions.Should().Be(0);
        limiter.ActivePins.Should().Be(0);
        using var next = limiter.AcquireTransaction();
    }
}
