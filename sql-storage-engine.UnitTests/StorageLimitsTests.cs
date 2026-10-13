using sql_storage_engine.Models.Resources;
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Diagnostics;
using AwesomeAssertions;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class StorageLimitsTests
{
    [Test]
    public void Defaults_ArePositiveAndWithinPublishedHardMaxima()
    {
        var limits = StorageLimitsExposure.CreateStorageLimits();
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
        ((Func<StorageLimits>)(() => StorageLimitsExposure.CreateStorageLimits(rowBytes: StorageLimits.MaximumRowBytes + 1))).Should().Throw<StorageModelValidationException>().Which.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
        ((Func<StorageLimits>)(() => StorageLimitsExposure.CreateStorageLimits(concurrentTransactions: 0))).Should().Throw<StorageModelValidationException>().Which.InnerException.Should().BeOfType<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ExceededLimit_UsesStableResourceErrorBeforeAllocation()
    {
        var limiter = new StorageResourceLimiterExposure(StorageLimitsExposure.CreateStorageLimits(rowBytes: 4));
        var allocated = false;
        var action = () =>
        {
            limiter.ValidateRowLength(5);
            allocated = true;
            _ = new byte[5];
        };
        var exception = action.Should().Throw<StorageResourceExhaustedException>().Which;
        exception.Message.Should().StartWith("ROW_BYTES limit 4 exceeded");
        allocated.Should().BeFalse();
    }

    [Test]
    public void FailureAndRelease_RestoreHeldResourceCapacity()
    {
        var limiter = new StorageResourceLimiterExposure(StorageLimitsExposure.CreateStorageLimits(concurrentTransactions: 1,pinsPerTransaction: 1));
        var transaction = limiter.AcquireTransaction();
        var pin = limiter.AcquirePin();
        try
        {
            ((Func<ResourceReservation>)limiter.AcquireTransaction).Should().Throw<StorageResourceExhaustedException>();
            ((Func<ResourceReservation>)limiter.AcquirePin).Should().Throw<StorageResourceExhaustedException>();
        }
        finally
        {
            limiter.ReleaseReservation(pin);
            limiter.ReleaseReservation(transaction);
        }

        limiter.GetActiveTransactions().Should().Be(0);
        limiter.GetActivePins().Should().Be(0);
        var next = limiter.AcquireTransaction();
        limiter.ReleaseReservation(next);
    }

    [Test]
    public void DirectlyInitializedInvalidLimitsAreRejectedAtResourceAdmission()
    {
        var limits = StorageLimitsExposure.CreateStorageLimits()with
        {
            RowBytes = 0
        };
        ((Func<StorageResourceLimiterExposure>)(() => new StorageResourceLimiterExposure(limits))).Should().Throw<StorageModelValidationException>().Which.GetBaseException().Should().BeOfType<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("rowBytes");
    }

    [TestCase(TimeSpan.TicksPerSecond - 1)]
    [TestCase(TimeSpan.TicksPerDay + 1)]
    public void DurationIsValidatedAtExactTickBoundaries(long ticks)
    {
        var limits = StorageLimitsExposure.CreateStorageLimits()with
        {
            TransactionDuration = TimeSpan.FromTicks(ticks)
        };
        ((Func<StorageLimits>)(() => StorageLimitsExposure.PrepareStorageLimits(limits))).Should().Throw<StorageModelValidationException>().Which.InnerException.Should().BeOfType<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("transactionDuration");
    }

    [Test]
    public void PublishedMaximaAreAcceptedWithoutAllocatingResourceCapacity()
    {
        var limits = StorageLimitsExposure.CreateStorageLimits(bufferFrames: StorageLimits.MaximumBufferFrames,rowBytes: StorageLimits.MaximumRowBytes,keyBytes: StorageLimits.MaximumKeyBytes,valueBytes: StorageLimits.MaximumValueBytes,overflowPages: StorageLimits.MaximumOverflowPages,transactionSeconds: StorageLimits.MaximumTransactionSeconds,undoBytes: StorageLimits.MaximumUndoBytes,pinsPerTransaction: StorageLimits.MaximumPinsPerTransaction,scanPages: StorageLimits.MaximumScanPages,concurrentTransactions: StorageLimits.MaximumConcurrentTransactions);
        StorageLimitsExposure.PrepareStorageLimits(limits).Should().BeSameAs(limits);
        var limiter = new StorageResourceLimiterExposure(limits);
        limiter.GetLimits().Should().BeSameAs(limits);
        limiter.GetActiveTransactions().Should().Be(0);
        limiter.GetActivePins().Should().Be(0);
        var minimumDuration = limits with
        {
            TransactionDuration = TimeSpan.FromSeconds(1)
        };
        StorageLimitsExposure.PrepareStorageLimits(minimumDuration).Should().BeSameAs(minimumDuration);
    }

    [Test]
    public void MissingLimitsAreRejectedWithTheOriginalArgumentCause()
    {
        ((Func<StorageLimits>)(() => StorageLimitsExposure.PrepareStorageLimits(null!))).Should().Throw<StorageModelValidationException>().Which.InnerException.Should().BeOfType<ArgumentNullException>().Which.ParamName.Should().Be("storageLimits");
    }
}