using AwesomeAssertions;
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Resources;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class ResourceCapacityTests
{
    [TestCase("row", "ROW_BYTES", 2L)]
    [TestCase("key", "KEY_BYTES", 3L)]
    [TestCase("value", "VALUE_BYTES", 4L)]
    [TestCase("overflow", "OVERFLOW_PAGES", 5L)]
    [TestCase("undo", "UNDO_BYTES", 6L)]
    [TestCase("scan", "SCAN_PAGES", 7L)]
    public void LengthQuotasKeepInclusiveBoundsAndStableResourceCodes(string kind, string code, long maximum)
    {
        var limiter = new StorageResourceLimiterExposure(StorageLimitsExposure.CreateStorageLimits(rowBytes: 2,keyBytes: 3,valueBytes: 4,overflowPages: 5,undoBytes: 6,scanPages: 7));
        Action<long> check = kind switch
        {
            "row" => limiter.ValidateRowLength,
            "key" => limiter.ValidateKeyLength,
            "value" => limiter.ValidateValueLength,
            "overflow" => limiter.ValidateOverflowPages,
            "undo" => limiter.ValidateUndoBytes,
            "scan" => limiter.ValidateScanPages,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))};
        check(0);
        check(maximum);
        foreach (var value in new[]
        {
            -1L,
            maximum + 1
        }

        )
        {
            var failure = ((Action)(() => check(value))).Should().Throw<StorageResourceExhaustedException>().Which;
            failure.Message.Should().StartWith($"{code} limit {maximum} exceeded by {value}.");
            failure.GetBaseException().Should().BeOfType<StorageResourceExhaustedException>();
            failure.InnerException.Should().NotBeNull();
        }

        limiter.GetActiveTransactions().Should().Be(0);
        limiter.GetActivePins().Should().Be(0);
    }

    [Test]
    public void ReservationCannotReleaseCapacityFromAnotherLimiter()
    {
        var first = new StorageResourceLimiterExposure(StorageLimitsExposure.CreateStorageLimits(concurrentTransactions: 1));
        var second = new StorageResourceLimiterExposure(StorageLimitsExposure.CreateStorageLimits(concurrentTransactions: 1));
        var reservation = first.AcquireTransaction();
        ((Action)(() => second.ReleaseReservation(reservation))).Should().Throw<StorageModelValidationException>().Which.GetBaseException().Should().BeOfType<ArgumentException>();
        first.GetActiveTransactions().Should().Be(1);
        second.GetActiveTransactions().Should().Be(0);
        first.ReleaseReservation(reservation);
        first.GetActiveTransactions().Should().Be(0);
    }

    [Test]
    public async Task ConcurrentDuplicateReleaseCannotStealAFutureReservation()
    {
        var limiter = new StorageResourceLimiterExposure(StorageLimitsExposure.CreateStorageLimits(concurrentTransactions: 1));
        var first = limiter.AcquireTransaction();
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => limiter.ReleaseReservation(first))));
        limiter.GetActiveTransactions().Should().Be(0);
        var second = limiter.AcquireTransaction();
        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => limiter.ReleaseReservation(first))));
        limiter.GetActiveTransactions().Should().Be(1);
        limiter.ReleaseReservation(second);
        limiter.GetActiveTransactions().Should().Be(0);
    }

    [Test]
    public async Task DeniedConcurrentAcquisitionsLeaveHeldCapacityUnchanged()
    {
        var limiter = new StorageResourceLimiterExposure(StorageLimitsExposure.CreateStorageLimits(concurrentTransactions: 1,pinsPerTransaction: 1));
        var transaction = limiter.AcquireTransaction();
        var pin = limiter.AcquirePin();
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            {
                ((Func<ResourceReservation>)limiter.AcquireTransaction).Should().Throw<StorageResourceExhaustedException>();
                ((Func<ResourceReservation>)limiter.AcquirePin).Should().Throw<StorageResourceExhaustedException>();
            })));
            limiter.GetActiveTransactions().Should().Be(1);
            limiter.GetActivePins().Should().Be(1);
        }
        finally
        {
            limiter.ReleaseReservation(pin);
            limiter.ReleaseReservation(transaction);
        }

        limiter.GetActiveTransactions().Should().Be(0);
        limiter.GetActivePins().Should().Be(0);
    }

    [Test]
    public void EmptyOrMissingReservationsAreRejectedWithoutChangingCapacity()
    {
        var limiter = new StorageResourceLimiterExposure(StorageLimitsExposure.CreateStorageLimits(concurrentTransactions: 1));
        var reservation = limiter.AcquireTransaction();
        ((Action)(() => limiter.ReleaseReservation(new ResourceReservation()))).Should().Throw<StorageModelValidationException>().Which.GetBaseException().Should().BeOfType<ArgumentNullException>();
        ((Action)(() => limiter.ReleaseReservation(null!))).Should().Throw<StorageModelValidationException>().Which.GetBaseException().Should().BeOfType<ArgumentNullException>();
        limiter.GetActiveTransactions().Should().Be(1);
        limiter.ReleaseReservation(reservation);
    }

    [Test]
    public void ConcurrentReservationsNeverAdmitMoreThanTheConfiguredCapacity()
    {
        var limiter = new StorageResourceLimiterExposure(StorageLimitsExposure.CreateStorageLimits(concurrentTransactions: 2));
        var held = 0;
        var admitted = 0;
        Parallel.For(0, 256, _ =>
        {
            ResourceReservation reservation;
            try
            {
                reservation = limiter.AcquireTransaction();
            }
            catch (StorageResourceExhaustedException)
            {
                return;
            }

            try
            {
                Interlocked.Increment(ref admitted);
                Interlocked.Increment(ref held).Should().BeLessThanOrEqualTo(2);
                Thread.Yield();
            }
            finally
            {
                Interlocked.Decrement(ref held);
                limiter.ReleaseReservation(reservation);
            }
        });
        admitted.Should().BeGreaterThan(0);
        held.Should().Be(0);
        limiter.GetActiveTransactions().Should().Be(0);
    }
}