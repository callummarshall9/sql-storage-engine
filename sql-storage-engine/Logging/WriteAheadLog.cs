using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Logging;

public interface IWalDevice
{
    long Length { get; }
    ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken = default);
    ValueTask<int> WriteAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default);
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
    ValueTask RollSegmentAsync(ulong segmentNumber, CancellationToken cancellationToken = default);
    void Truncate(long length);
}

/// <summary>Sequential, thread-safe WAL appender with complete-write, rollover, and flush-through semantics.</summary>
public sealed class WriteAheadLog
{
    private readonly IWalDevice _device;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly int _maximumSegmentBytes;
    private long _length;
    private int _segmentBytes;
    private ulong _segmentNumber;
    private ulong _durableLsn;

    private WriteAheadLog(IWalDevice device, long length, int maximumSegmentBytes,
        int segmentBytes, ulong segmentNumber)
    {
        _device = device;
        _length = length;
        _maximumSegmentBytes = maximumSegmentBytes;
        _segmentBytes = segmentBytes;
        _segmentNumber = segmentNumber;
    }

    public LogSequenceNumber DurableLsn => new(Volatile.Read(ref _durableLsn));
    public ulong CurrentSegmentNumber => _segmentNumber;

    public static async ValueTask<WriteAheadLog> OpenAsync(IWalDevice device,
        int maximumSegmentBytes = 16 * 1024 * 1024, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (maximumSegmentBytes < WalFormat.RecordHeaderLength) throw new ArgumentOutOfRangeException(nameof(maximumSegmentBytes));
        if (device.Length > int.MaxValue) throw new StorageResourceExhaustedException("WAL exceeds the reopen scan bound.");
        var bytes = new byte[checked((int)device.Length)];
        var read = 0;
        while (read < bytes.Length)
        {
            var count = await device.ReadAsync(read, bytes.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (count <= 0) throw new StorageFormatException("WAL device returned a short reopen read.");
            read = checked(read + count);
        }
        var result = WalFormat.ReadRecords(bytes);
        if (result.HasIncompleteTail) device.Truncate(result.ValidLength);
        var segment = 0UL;
        var segmentBytes = 0;
        foreach (var record in result.Records)
        {
            var length = WalFormat.WriteRecord(record).Length;
            if (segmentBytes > 0 && segmentBytes + length > maximumSegmentBytes) { segment++; segmentBytes = 0; }
            segmentBytes += length;
        }
        return new WriteAheadLog(device, result.ValidLength, maximumSegmentBytes, segmentBytes, segment);
    }

    public async ValueTask<WalRecord> AppendAsync(TransactionId transactionId, WalRecordType type,
        LogSequenceNumber previousLsn, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var provisional = new WalRecord(new LogSequenceNumber(checked((ulong)_length + 1)), previousLsn,
                transactionId, type, payload);
            var bytes = WalFormat.WriteRecord(provisional);
            if (bytes.Length > _maximumSegmentBytes) throw new StorageResourceExhaustedException("WAL record exceeds segment capacity.");
            if (_segmentBytes > 0 && _segmentBytes + bytes.Length > _maximumSegmentBytes)
            {
                await _device.RollSegmentAsync(++_segmentNumber, cancellationToken).ConfigureAwait(false);
                _segmentBytes = 0;
            }
            var written = 0;
            while (written < bytes.Length)
            {
                var count = await _device.WriteAsync(_length + written, bytes.AsMemory(written), cancellationToken)
                    .ConfigureAwait(false);
                if (count <= 0) throw new StorageResourceException("WAL append made no write progress.", new IOException());
                written = checked(written + count);
            }
            _length = checked(_length + bytes.Length);
            _segmentBytes = checked(_segmentBytes + bytes.Length);
            return provisional;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask FlushThroughAsync(LogSequenceNumber lsn, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (lsn.Value == 0 || lsn.Value > checked((ulong)_length)) throw new ArgumentOutOfRangeException(nameof(lsn));
            await _device.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (lsn.Value > _durableLsn) Volatile.Write(ref _durableLsn, lsn.Value);
        }
        finally { _gate.Release(); }
    }
}
