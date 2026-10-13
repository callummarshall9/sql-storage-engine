// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Diagnostics.Metrics;
using System;
using System.Threading;
using System.Collections.Generic;

namespace sql_storage_engine.Dependencies.Metrics;
/// <summary>
/// Publishes bounded-cardinality storage metrics. Tags are limited to fixed operation and outcome values; logical
/// identifiers, row values, and index keys are never labels.
/// </summary>
public sealed class StorageMetricsDependency : IDisposable
{
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
    public StorageMetricsDependency(string? meterVersion = null)
    {
        _meter = new Meter("sql-storage-engine", meterVersion);
        _bufferHits = _meter.CreateCounter<long>(name: "storage.buffer.hits");
        _bufferMisses = _meter.CreateCounter<long>(name: "storage.buffer.misses");
        _pageReads = _meter.CreateCounter<long>(name: "storage.page.reads");
        _pageWrites = _meter.CreateCounter<long>(name: "storage.page.writes");
        _pageFlushDuration = _meter.CreateHistogram<double>(name: "storage.page.flush.duration", unit: "ms");
        _walBytes = _meter.CreateCounter<long>(name: "storage.wal.bytes");
        _walFlushDuration = _meter.CreateHistogram<double>(name: "storage.wal.flush.duration", unit: "ms");
        _transactions = _meter.CreateCounter<long>(name: "storage.transactions");
        _deadlocks = _meter.CreateCounter<long>(name: "storage.transactions.deadlocks");
        _bTreeSplits = _meter.CreateCounter<long>(name: "storage.btree.splits");
        _bTreeMerges = _meter.CreateCounter<long>(name: "storage.btree.merges");
        _checkpointDuration = _meter.CreateHistogram<double>(name: "storage.checkpoint.duration", unit: "ms");
        _meter.CreateObservableGauge(name: "storage.buffer.pinned", observeValue: () => Interlocked.Read(location: ref _pinnedFrames));
        _meter.CreateObservableGauge(name: "storage.buffer.dirty", observeValue: () => Interlocked.Read(location: ref _dirtyFrames));
        _meter.CreateObservableGauge(name: "storage.heap.live.bytes", observeValue: () => Interlocked.Read(location: ref _heapLiveBytes));
        _meter.CreateObservableGauge(name: "storage.heap.dead.bytes", observeValue: () => Interlocked.Read(location: ref _heapDeadBytes));
        _meter.CreateObservableGauge(name: "storage.btree.height", observeValue: () => Interlocked.Read(location: ref _bTreeHeight));
        _meter.CreateObservableGauge(name: "storage.overflow.bytes", observeValue: () => Interlocked.Read(location: ref _overflowBytes));
        _meter.CreateObservableGauge(name: "storage.recovery.distance", observeValue: () => Interlocked.Read(location: ref _recoveryDistance));
    }

    public void RecordBufferAccess(bool hit) =>
        (hit ? _bufferHits : _bufferMisses).Add(delta: 1);

    public void RecordPageRead(bool succeeded) =>
        _pageReads.Add(delta: 1, tag: Outcome(succeeded: succeeded));

    public void RecordPageWrite(bool succeeded) =>
        _pageWrites.Add(delta: 1, tag: Outcome(succeeded: succeeded));

    public void RecordPageFlush(TimeSpan duration, bool succeeded) =>
        _pageFlushDuration.Record(value: duration.TotalMilliseconds, tag: Outcome(succeeded: succeeded));

    public void RecordWalAppend(int bytes, bool succeeded)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value: bytes);
        _walBytes.Add(delta: bytes, tag: Outcome(succeeded: succeeded));
    }

    public void RecordWalFlush(TimeSpan duration, bool succeeded) =>
        _walFlushDuration.Record(value: duration.TotalMilliseconds, tag: Outcome(succeeded: succeeded));

    public void RecordTransaction(string outcome)
    {
        if (outcome is not ("commit" or "rollback"))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        _transactions.Add(delta: 1, tag: new KeyValuePair<string, object?>("outcome", outcome));
    }

    public void RecordDeadlock() =>
        _deadlocks.Add(delta: 1);

    public void RecordBTreeSplit() =>
        _bTreeSplits.Add(delta: 1);

    public void RecordBTreeMerge() =>
        _bTreeMerges.Add(delta: 1);

    public void RecordCheckpoint(TimeSpan duration, bool succeeded) =>
        _checkpointDuration.Record(value: duration.TotalMilliseconds, tag: Outcome(succeeded: succeeded));

    public void SetBufferFrames(int pinned, int dirty)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value: pinned);
        ArgumentOutOfRangeException.ThrowIfNegative(value: dirty);
        Interlocked.Exchange(location1: ref _pinnedFrames, value: pinned);
        Interlocked.Exchange(location1: ref _dirtyFrames, value: dirty);
    }

    public void SetHeapBytes(long live, long dead)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value: live);
        ArgumentOutOfRangeException.ThrowIfNegative(value: dead);
        Interlocked.Exchange(location1: ref _heapLiveBytes, value: live);
        Interlocked.Exchange(location1: ref _heapDeadBytes, value: dead);
    }

    public void SetBTreeHeight(int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value: height);
        Interlocked.Exchange(location1: ref _bTreeHeight, value: height);
    }

    public void SetOverflowBytes(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value: bytes);
        Interlocked.Exchange(location1: ref _overflowBytes, value: bytes);
    }

    public void SetRecoveryDistance(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value: bytes);
        Interlocked.Exchange(location1: ref _recoveryDistance, value: bytes);
    }

    public void Dispose() =>
        _meter.Dispose();

    private static KeyValuePair<string, object?> Outcome(bool succeeded) =>
        new("outcome", succeeded ? "success" : "failure");
}