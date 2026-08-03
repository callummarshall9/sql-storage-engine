using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Logging;

namespace sql_storage_engine.UnitTests;

public sealed class WriteAheadLogTests
{
    [Test]
    public async Task AppendAssignsIncreasingLsnsAndCompletesShortWrites()
    {
        var device = new MemoryWalDevice { MaximumWrite = 3 };
        var wal = await WriteAheadLog.OpenAsync(device);
        var first = await wal.AppendAsync(new TransactionId(1), WalRecordType.Begin, default, ReadOnlyMemory<byte>.Empty);
        var second = await wal.AppendAsync(new TransactionId(1), WalRecordType.Commit, first.Lsn, ReadOnlyMemory<byte>.Empty);
        second.Lsn.Value.Should().BeGreaterThan(first.Lsn.Value);
        device.WriteCalls.Should().BeGreaterThan(2);
    }

    [Test]
    public async Task FlushFailureDoesNotAdvanceDurabilityAndSuccessConfirmsRequestedLsn()
    {
        var device = new MemoryWalDevice();
        var wal = await WriteAheadLog.OpenAsync(device);
        var record = await wal.AppendAsync(new TransactionId(1), WalRecordType.Begin, default, ReadOnlyMemory<byte>.Empty);
        device.FailFlush = true;
        await ((Func<Task>)(async () => await wal.FlushThroughAsync(record.Lsn))).Should().ThrowAsync<IOException>();
        wal.DurableLsn.Should().Be(default(LogSequenceNumber));
        device.FailFlush = false;
        await wal.FlushThroughAsync(record.Lsn);
        wal.DurableLsn.Should().Be(record.Lsn);
    }

    [Test]
    public async Task ReopenFindsLastCompleteRecordAndTruncatesIncompleteTail()
    {
        var device = new MemoryWalDevice();
        var wal = await WriteAheadLog.OpenAsync(device);
        var first = await wal.AppendAsync(new TransactionId(1), WalRecordType.Begin, default, ReadOnlyMemory<byte>.Empty);
        device.AppendRaw([50, 0, 0, 0, 1]);
        var reopened = await WriteAheadLog.OpenAsync(device);
        var next = await reopened.AppendAsync(new TransactionId(2), WalRecordType.Begin, default, ReadOnlyMemory<byte>.Empty);
        next.Lsn.Value.Should().BeGreaterThan(first.Lsn.Value);
        device.TruncateCalls.Should().Be(1);
    }

    [Test]
    public async Task SegmentRolloverPreservesGlobalRecordOrder()
    {
        var device = new MemoryWalDevice();
        var wal = await WriteAheadLog.OpenAsync(device, WalFormat.RecordHeaderLength + 1);
        var first = await wal.AppendAsync(new TransactionId(1), WalRecordType.Begin, default, new byte[] { 1 });
        var second = await wal.AppendAsync(new TransactionId(1), WalRecordType.Commit, first.Lsn, new byte[] { 2 });
        wal.CurrentSegmentNumber.Should().Be(1);
        device.RolledSegments.Should().Equal(1UL);
        WalFormat.ReadRecords(device.Bytes).Records.Select(record => record.Lsn).Should().Equal(first.Lsn, second.Lsn);
    }

    internal class MemoryWalDevice : IWalDevice
    {
        private readonly List<byte> _bytes = [];
        public int MaximumWrite { get; set; } = int.MaxValue;
        public bool FailFlush { get; set; }
        public int WriteCalls { get; private set; }
        public int TruncateCalls { get; private set; }
        public List<ulong> RolledSegments { get; } = [];
        public long Length => _bytes.Count;
        public byte[] Bytes => _bytes.ToArray();
        public ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken = default)
        {
            var count = Math.Min(destination.Length, _bytes.Count - checked((int)offset));
            for (var index = 0; index < count; index++) destination.Span[index] = _bytes[checked((int)offset) + index];
            return ValueTask.FromResult(count);
        }
        public ValueTask<int> WriteAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            var count = Math.Min(MaximumWrite, source.Length);
            while (_bytes.Count < offset + count) _bytes.Add(0);
            for (var index = 0; index < count; index++) _bytes[checked((int)offset) + index] = source.Span[index];
            return ValueTask.FromResult(count);
        }
        public virtual ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
            FailFlush ? ValueTask.FromException(new IOException("Injected flush failure.")) : ValueTask.CompletedTask;
        public ValueTask RollSegmentAsync(ulong segmentNumber, CancellationToken cancellationToken = default)
        { RolledSegments.Add(segmentNumber); return ValueTask.CompletedTask; }
        public void Truncate(long length) { _bytes.RemoveRange(checked((int)length), _bytes.Count - checked((int)length)); TruncateCalls++; }
        public void AppendRaw(IEnumerable<byte> bytes) => _bytes.AddRange(bytes);
    }
}
