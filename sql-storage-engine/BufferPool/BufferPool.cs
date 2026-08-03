using System.Diagnostics.Metrics;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Buffers;

/// <summary>A bounded, thread-safe cache of complete database pages.</summary>
public sealed class BufferPool : IAsyncDisposable
{
    private static readonly Meter Meter = new("sql-storage-engine.buffer-pool");
    private static readonly Counter<long> HitMetric = Meter.CreateCounter<long>("buffer_pool.hits");
    private static readonly Counter<long> MissMetric = Meter.CreateCounter<long>("buffer_pool.misses");

    private readonly IPageStore _pageStore;
    private readonly bool _leaveOpen;
    private readonly IPageFlushGuard _flushGuard;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<PageId, BufferFrame> _frames = [];
    private readonly List<BufferFrame> _clock = [];
    private int _clockHand;
    private long _hitCount;
    private long _missCount;
    private bool _disposed;

    public BufferPool(IPageStore pageStore, int capacity, bool leaveOpen = false, IPageFlushGuard? flushGuard = null)
    {
        ArgumentNullException.ThrowIfNull(pageStore);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _pageStore = pageStore;
        Capacity = capacity;
        _leaveOpen = leaveOpen;
        _flushGuard = flushGuard ?? NoOpPageFlushGuard.Instance;
    }

    public int Capacity { get; }
    public int PageSize => _pageStore.PageSize;
    public int FrameCount { get { _gate.Wait(); try { return _frames.Count; } finally { _gate.Release(); } } }
    public long HitCount => Interlocked.Read(ref _hitCount);
    public long MissCount => Interlocked.Read(ref _missCount);
    public int PinnedPageCount
    {
        get
        {
            _gate.Wait();
            try { return _frames.Values.Sum(frame => frame.PinCount); }
            finally { _gate.Release(); }
        }
    }

    /// <summary>Returns a pinned cached page, loading one complete page on a cache miss.</summary>
    public async ValueTask<IPinnedPage> GetPageAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_frames.TryGetValue(pageId, out var existing))
            {
                Interlocked.Increment(ref _hitCount);
                HitMetric.Add(1);
                return existing.Pin();
            }

            Interlocked.Increment(ref _missCount);
            MissMetric.Add(1);
            var bytes = new byte[_pageStore.PageSize];
            BufferFrame? victim = null;
            if (_frames.Count == Capacity) victim = SelectVictim();
            if (victim is not null) await FlushFrameAsync(victim, cancellationToken).ConfigureAwait(false);
            await _pageStore.ReadAsync(pageId, bytes, cancellationToken).ConfigureAwait(false);
            BufferFrame loaded;
            if (victim is null)
            {
                loaded = new BufferFrame(pageId, bytes);
                _clock.Add(loaded);
            }
            else
            {
                _frames.Remove(victim.PageId);
                victim.Reset(pageId, bytes);
                loaded = victim;
            }
            _frames.Add(pageId, loaded);
            return loaded.Pin();
        }
        finally { _gate.Release(); }
    }

    /// <summary>Flushes one cached dirty page. Pinned pages are flushed from a stable snapshot.</summary>
    public async ValueTask FlushPageAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!_frames.TryGetValue(pageId, out var frame))
                throw new StorageResourceException($"Cannot flush uncached {pageId}.", new KeyNotFoundException());
            await FlushFrameAsync(frame, cancellationToken).ConfigureAwait(false);
            await _pageStore.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Flushes all dirty frames and then the backing store. Pinned frames are included using snapshots;
    /// a page marked dirty again during its write remains dirty.
    /// </summary>
    public async ValueTask FlushAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            foreach (var frame in _clock) await FlushFrameAsync(frame, cancellationToken).ConfigureAwait(false);
            await _pageStore.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Removes an unpinned cached page without flushing it, for allocation rollback.</summary>
    public async ValueTask DiscardPageAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!_frames.TryGetValue(pageId, out var frame)) return;
            if (!frame.IsEvictable) throw new StorageResourceException($"Cannot discard pinned {pageId}.", new InvalidOperationException());
            _frames.Remove(pageId);
            var index = _clock.IndexOf(frame);
            if (index >= 0)
            {
                _clock.RemoveAt(index);
                if (_clock.Count == 0) _clockHand = 0;
                else
                {
                    if (index < _clockHand) _clockHand--;
                    _clockHand %= _clock.Count;
                }
            }
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            if (_frames.Values.Any(frame => !frame.IsEvictable))
                throw new StorageResourceException("Cannot dispose a buffer pool while pages are pinned.", new InvalidOperationException());
            foreach (var frame in _clock) await FlushFrameAsync(frame, CancellationToken.None).ConfigureAwait(false);
            await _pageStore.FlushAsync().ConfigureAwait(false);
            _disposed = true;
            _frames.Clear();
            _clock.Clear();
        }
        finally { _gate.Release(); }
        if (!_leaveOpen) await _pageStore.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private BufferFrame SelectVictim()
    {
        // Two revolutions are sufficient: the first clears reference bits and the second selects.
        for (var inspected = 0; inspected < _clock.Count * 2; inspected++)
        {
            var candidate = _clock[_clockHand];
            _clockHand = (_clockHand + 1) % _clock.Count;
            if (!candidate.GiveSecondChance()) return candidate;
        }
        throw new StorageResourceExhaustedException(
            $"Buffer pool capacity {Capacity} is exhausted because every frame is pinned.");
    }

    private async ValueTask FlushFrameAsync(BufferFrame frame, CancellationToken cancellationToken)
    {
        if (!frame.IsDirty) return;
        var snapshot = frame.CaptureFlushSnapshot();
        await _flushGuard.EnsureCanFlushAsync(frame.PageId, snapshot.PageLogSequenceNumber, cancellationToken)
            .ConfigureAwait(false);
        PageChecksum.WriteChecksum(snapshot.Bytes, _pageStore.PageSize);
        await _pageStore.WriteAsync(frame.PageId, snapshot.Bytes, cancellationToken).ConfigureAwait(false);
        frame.CompleteFlush(snapshot.DirtyGeneration);
    }
}
