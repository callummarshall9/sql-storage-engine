using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Buffers;

/// <summary>Owns one fixed-size cached page and its eviction-safety metadata.</summary>
public sealed class BufferFrame
{
    private readonly object _sync = new();
    private PageId _pageId;
    private byte[] _bytes;
    private int _pinCount;
    private bool _isDirty;
    private LogSequenceNumber _pageLogSequenceNumber;
    private bool _recentlyUsed = true;
    private ulong _dirtyGeneration;

    public BufferFrame(PageId pageId, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0) throw new ArgumentException("A buffer frame cannot be empty.", nameof(bytes));
        _pageId = pageId;
        _bytes = bytes;
    }

    public PageId PageId { get { lock (_sync) return _pageId; } }
    public int PinCount { get { lock (_sync) return _pinCount; } }
    public bool IsDirty { get { lock (_sync) return _isDirty; } }
    public bool IsEvictable { get { lock (_sync) return _pinCount == 0; } }
    public LogSequenceNumber PageLogSequenceNumber { get { lock (_sync) return _pageLogSequenceNumber; } }

    /// <summary>Acquires an ownership handle and increments the pin count.</summary>
    public IPinnedPage Pin()
    {
        lock (_sync)
        {
            checked { _pinCount++; }
            _recentlyUsed = true;
            return new PinnedPage(this);
        }
    }

    internal Memory<byte> GetMemory()
    {
        lock (_sync) return _bytes;
    }

    internal void MarkDirty(LogSequenceNumber pageLogSequenceNumber)
    {
        lock (_sync)
        {
            _isDirty = true;
            _pageLogSequenceNumber = pageLogSequenceNumber;
            checked { _dirtyGeneration++; }
        }
    }

    internal (byte[] Bytes, ulong DirtyGeneration, LogSequenceNumber PageLogSequenceNumber) CaptureFlushSnapshot()
    {
        lock (_sync) return (_bytes.ToArray(), _dirtyGeneration, _pageLogSequenceNumber);
    }

    internal void CompleteFlush(ulong dirtyGeneration)
    {
        lock (_sync)
        {
            // A pin may mark the page dirty again while its earlier snapshot is being written.
            if (_dirtyGeneration == dirtyGeneration) _isDirty = false;
        }
    }

    internal void Reset(PageId pageId, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        lock (_sync)
        {
            if (_pinCount != 0) throw new InvalidOperationException("A pinned frame cannot be reassigned.");
            _pageId = pageId;
            _bytes = bytes;
            _isDirty = false;
            _pageLogSequenceNumber = default;
            _recentlyUsed = true;
            _dirtyGeneration = 0;
        }
    }

    internal bool GiveSecondChance()
    {
        lock (_sync)
        {
            if (_pinCount != 0) return true;
            if (!_recentlyUsed) return false;
            _recentlyUsed = false;
            return true;
        }
    }

    private void Unpin()
    {
        lock (_sync)
        {
            if (_pinCount == 0) throw new InvalidOperationException("The frame has no pin to release.");
            _pinCount--;
        }
    }

    private sealed class PinnedPage(BufferFrame frame) : IPinnedPage
    {
        private bool _disposed;

        public PageId PageId { get { ThrowIfDisposed(); return frame.PageId; } }
        public Memory<byte> Memory { get { ThrowIfDisposed(); return frame.GetMemory(); } }
        public LogSequenceNumber PageLogSequenceNumber { get { ThrowIfDisposed(); return frame.PageLogSequenceNumber; } }
        public bool IsDirty { get { ThrowIfDisposed(); return frame.IsDirty; } }

        public void MarkDirty(LogSequenceNumber pageLogSequenceNumber)
        {
            ThrowIfDisposed();
            frame.MarkDirty(pageLogSequenceNumber);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            frame.Unpin();
        }

        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
