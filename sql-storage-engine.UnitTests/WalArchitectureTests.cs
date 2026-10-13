// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Exposures.Logging;
using sql_storage_engine.Logging;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Logging;

namespace sql_storage_engine.UnitTests;
public sealed class WalArchitectureTests
{
    [Test]
    public async Task ConcurrentAppendsKeepCompleteRecordsAndUniqueOrderedOffsets()
    {
        var device = new YieldingWalDevice();
        var wal = await WriteAheadLogExposure.OpenAsync(device);
        var records = await Task.WhenAll(Enumerable.Range(1, 100).Select(async number =>
            await wal.AppendAsync(new TransactionId { Value = (ulong)number },WalRecordType.Begin,default,new byte[] { (byte)number })));

        var decoded = WalReadResultExposure.ReadWalReadResult(device.Bytes);
        decoded.HasIncompleteTail.Should().BeFalse();
        decoded.Records.Count.Should().Be(100);
        decoded.Records.Select(record => record.Lsn.Value).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        decoded.Records.Select(record => record.TransactionId).Should().BeEquivalentTo(records.Select(record => record.TransactionId));
        await wal.FlushThroughAsync(records.MaxBy(record => record.Lsn.Value)!.Lsn);
        wal.ReadDurableLsn().Value.Should().Be(records.Max(record => record.Lsn.Value));
    }

    [Test]
    public async Task CancelledFlushPreservesDurabilityAndReleasesAdmissionForLaterWork()
    {
        var device = new CancellingWalDevice();
        var wal = await WriteAheadLogExposure.OpenAsync(device);
        var first = await wal.AppendAsync(new TransactionId { Value = 1 }, WalRecordType.Begin, default, ReadOnlyMemory<byte>.Empty);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = await ((Func<Task>)(async () => await wal.FlushThroughAsync(first.Lsn, cancellation.Token)))
            .Should().ThrowAsync<OperationCanceledException>();
        error.Which.CancellationToken.Should().Be(cancellation.Token);
        wal.ReadDurableLsn().Value.Should().Be(0);
        var second = await wal.AppendAsync(new TransactionId { Value = 2 }, WalRecordType.Begin, default, ReadOnlyMemory<byte>.Empty);
        await wal.EnsureCanFlushAsync(default, second.Lsn);
        wal.ReadDurableLsn().Should().Be(second.Lsn);
    }

    [Test]
    public async Task FailedCheckpointFlushCannotPublishAndSuccessfulPublicationObservesDurability()
    {
        var device = new WriteAheadLogTests.MemoryWalDevice { FailFlush = true };
        var wal = await WriteAheadLogExposure.OpenAsync(device);
        var reference = new ObservingCheckpointReference(wal);
        var checkpoint = new CheckpointState { ActiveTransactions = new Dictionary<TransactionId, LogSequenceNumber>(), DirtyPages = new Dictionary<PageId, LogSequenceNumber>() };
        await ((Func<Task>)(async () => await wal.CreateCheckpointLogSequenceNumberAsync(checkpoint,reference))).Should().ThrowAsync<IOException>();
        reference.Publications.Should().Be(0);
        wal.ReadDurableLsn().Value.Should().Be(0);
        device.FailFlush = false;
        var lsn = await wal.CreateCheckpointLogSequenceNumberAsync(checkpoint,reference);
        reference.ReadLatestCheckpointLsn().Should().Be(lsn);
        reference.Publications.Should().Be(1);
        reference.DurabilityObserved.Should().BeTrue();
    }

    private sealed class ObservingCheckpointReference(WriteAheadLogExposure wal) : ICheckpointReference
    {
        private LogSequenceNumber? latest;
        public int Publications { get; private set; }
        public bool DurabilityObserved { get; private set; }
        public LogSequenceNumber? ReadLatestCheckpointLsn() => latest;
        public ValueTask PublishAsync(LogSequenceNumber checkpointLsn, CancellationToken cancellationToken = default)
        {
            Publications++;
            latest = checkpointLsn;
            DurabilityObserved = wal.ReadDurableLsn().Value >= checkpointLsn.Value;
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task InvalidProviderWriteCountCannotAdvanceTheLogicalWal()
    {
        var device = new InvalidCountWalDevice { InvalidWrites = true };
        var wal = await WriteAheadLogExposure.OpenAsync(device);
        await ((Func<Task>)(async () => await wal.AppendAsync(new TransactionId { Value = 1 }, WalRecordType.Begin, default, ReadOnlyMemory<byte>.Empty)))
            .Should().ThrowAsync<sql_storage_engine.Storage.StorageResourceException>();
        wal.ReadDurableLsn().Value.Should().Be(0);
        device.InvalidWrites = false;
        var first = await wal.AppendAsync(new TransactionId { Value = 1 }, WalRecordType.Begin, default, ReadOnlyMemory<byte>.Empty);
        first.Lsn.Value.Should().Be(1);
    }

    [Test]
    public async Task InvalidProviderReadCountCannotBeAcceptedAsACompleteReopen()
    {
        var device = new InvalidCountWalDevice();
        var wal = await WriteAheadLogExposure.OpenAsync(device);
        await wal.AppendAsync(new TransactionId { Value = 1 }, WalRecordType.Begin, default, ReadOnlyMemory<byte>.Empty);
        device.InvalidReads = true;
        await ((Func<Task>)(async () => await WriteAheadLogExposure.OpenAsync(device)))
            .Should().ThrowAsync<sql_storage_engine.Storage.StorageFormatException>();
    }

    private sealed class InvalidCountWalDevice : WriteAheadLogTests.MemoryWalDevice
    {
        public bool InvalidWrites { get; set; }
        public bool InvalidReads { get; set; }
        public override ValueTask<int> WriteAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) =>
            InvalidWrites ? ValueTask.FromResult(source.Length + 1) : base.WriteAsync(offset,source,cancellationToken);
        public override ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken = default) =>
            InvalidReads ? ValueTask.FromResult(destination.Length + 1) : base.ReadAsync(offset,destination,cancellationToken);
    }

    private sealed class YieldingWalDevice : WriteAheadLogTests.MemoryWalDevice
    {
        public YieldingWalDevice() => MaximumWrite = 3;
        public override async ValueTask<int> WriteAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return await base.WriteAsync(offset,source,cancellationToken);
        }
    }

    private sealed class CancellingWalDevice : WriteAheadLogTests.MemoryWalDevice
    {
        public override ValueTask FlushAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}