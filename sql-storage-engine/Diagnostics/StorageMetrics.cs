using System.Diagnostics.Metrics;

namespace sql_storage_engine.Diagnostics;

/// <summary>
/// Publishes bounded-cardinality storage metrics. Tags are limited to fixed operation and outcome values; logical
/// identifiers, row values, and index keys are never labels.
/// </summary>
public sealed class StorageMetrics : IDisposable
{
    public const string MeterName = "sql-storage-engine";
    private readonly Meter _meter;
    private readonly Counter<long> _bufferHits;
    private readonly Counter<long> _bufferMisses;
    private readonly Counter<long> _pageReads;
    private readonly Counter<long> _pageWrites;
    private readonly Histogram<double> _pageFlushDuration;
    private readonly Counter<long> _walBytes;
    private readonly Histogram<double> _walFlushDuration;
    private readonly Counter<long> _transactions;
    private readonly Counter<long> _deadlocks;
    private readonly Counter<long> _bTreeSplits;
    private readonly Counter<long> _bTreeMerges;
    private readonly Histogram<double> _checkpointDuration;
    private long _pinnedFrames;
    private long _dirtyFrames;
    private long _heapLiveBytes;
    private long _heapDeadBytes;
    private long _bTreeHeight;
    private long _overflowBytes;
    private long _recoveryDistance;

    public StorageMetrics(string? meterVersion = null)
    {
        _meter = new Meter(MeterName, meterVersion);
        _bufferHits = _meter.CreateCounter<long>("storage.buffer.hits");
        _bufferMisses = _meter.CreateCounter<long>("storage.buffer.misses");
        _pageReads = _meter.CreateCounter<long>("storage.page.reads");
        _pageWrites = _meter.CreateCounter<long>("storage.page.writes");
        _pageFlushDuration = _meter.CreateHistogram<double>("storage.page.flush.duration", "ms");
        _walBytes = _meter.CreateCounter<long>("storage.wal.bytes");
        _walFlushDuration = _meter.CreateHistogram<double>("storage.wal.flush.duration", "ms");
        _transactions = _meter.CreateCounter<long>("storage.transactions");
        _deadlocks = _meter.CreateCounter<long>("storage.transactions.deadlocks");
        _bTreeSplits = _meter.CreateCounter<long>("storage.btree.splits");
        _bTreeMerges = _meter.CreateCounter<long>("storage.btree.merges");
        _checkpointDuration = _meter.CreateHistogram<double>("storage.checkpoint.duration", "ms");
        _meter.CreateObservableGauge("storage.buffer.pinned", () => Interlocked.Read(ref _pinnedFrames));
        _meter.CreateObservableGauge("storage.buffer.dirty", () => Interlocked.Read(ref _dirtyFrames));
        _meter.CreateObservableGauge("storage.heap.live.bytes", () => Interlocked.Read(ref _heapLiveBytes));
        _meter.CreateObservableGauge("storage.heap.dead.bytes", () => Interlocked.Read(ref _heapDeadBytes));
        _meter.CreateObservableGauge("storage.btree.height", () => Interlocked.Read(ref _bTreeHeight));
        _meter.CreateObservableGauge("storage.overflow.bytes", () => Interlocked.Read(ref _overflowBytes));
        _meter.CreateObservableGauge("storage.recovery.distance", () => Interlocked.Read(ref _recoveryDistance));
    }

    public void RecordBufferAccess(bool hit) => (hit ? _bufferHits : _bufferMisses).Add(1);
    public void RecordPageRead(bool succeeded) => _pageReads.Add(1, Outcome(succeeded));
    public void RecordPageWrite(bool succeeded) => _pageWrites.Add(1, Outcome(succeeded));
    public void RecordPageFlush(TimeSpan duration, bool succeeded) =>
        _pageFlushDuration.Record(duration.TotalMilliseconds, Outcome(succeeded));
    public void RecordWalAppend(int bytes, bool succeeded)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        _walBytes.Add(bytes, Outcome(succeeded));
    }
    public void RecordWalFlush(TimeSpan duration, bool succeeded) =>
        _walFlushDuration.Record(duration.TotalMilliseconds, Outcome(succeeded));
    public void RecordTransaction(string outcome)
    {
        if (outcome is not ("commit" or "rollback")) throw new ArgumentOutOfRangeException(nameof(outcome));
        _transactions.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
    public void RecordDeadlock() => _deadlocks.Add(1);
    public void RecordBTreeSplit() => _bTreeSplits.Add(1);
    public void RecordBTreeMerge() => _bTreeMerges.Add(1);
    public void RecordCheckpoint(TimeSpan duration, bool succeeded) =>
        _checkpointDuration.Record(duration.TotalMilliseconds, Outcome(succeeded));
    public void SetBufferFrames(int pinned, int dirty)
    { ArgumentOutOfRangeException.ThrowIfNegative(pinned); ArgumentOutOfRangeException.ThrowIfNegative(dirty); Interlocked.Exchange(ref _pinnedFrames, pinned); Interlocked.Exchange(ref _dirtyFrames, dirty); }
    public void SetHeapBytes(long live, long dead)
    { ArgumentOutOfRangeException.ThrowIfNegative(live); ArgumentOutOfRangeException.ThrowIfNegative(dead); Interlocked.Exchange(ref _heapLiveBytes, live); Interlocked.Exchange(ref _heapDeadBytes, dead); }
    public void SetBTreeHeight(int height) { ArgumentOutOfRangeException.ThrowIfNegative(height); Interlocked.Exchange(ref _bTreeHeight, height); }
    public void SetOverflowBytes(long bytes) { ArgumentOutOfRangeException.ThrowIfNegative(bytes); Interlocked.Exchange(ref _overflowBytes, bytes); }
    public void SetRecoveryDistance(long bytes) { ArgumentOutOfRangeException.ThrowIfNegative(bytes); Interlocked.Exchange(ref _recoveryDistance, bytes); }
    public void Dispose() => _meter.Dispose();

    private static KeyValuePair<string, object?> Outcome(bool succeeded) => new("outcome", succeeded ? "success" : "failure");
}
