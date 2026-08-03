using sql_storage_engine.Identifiers;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Pages;

/// <summary>A thread-safe, copying page store for deterministic tests.</summary>
public sealed class InMemoryPageStore : IPageStore, IPageAllocator
{
    private readonly object _sync = new();
    private readonly Dictionary<PageId, byte[]> _pages = [];
    private readonly Stack<PageId> _free = [];
    private ulong _nextPageId;
    private bool _disposed;

    public InMemoryPageStore(int pageSize = PageConstants.DefaultSize, bool reservePageZero = true)
    {
        if (!PageConstants.IsSupportedSize(pageSize)) throw new ArgumentOutOfRangeException(nameof(pageSize));
        PageSize = pageSize;
        _nextPageId = reservePageZero ? 1UL : 0UL;
    }

    public int PageSize { get; }

    public ValueTask<PageId> AllocateAsync(PageType pageType, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(pageType) || pageType == PageType.Unknown) throw new ArgumentOutOfRangeException(nameof(pageType));
            var id = _free.TryPop(out var reused) ? reused : new PageId(checked(_nextPageId++));
            var page = new byte[PageSize];
            PageHeaderCodec.Write(page, new PageHeader(id, pageType, PageFormatVersion.Current, new LogSequenceNumber(0), PageChecksumAlgorithm.Crc32, 0));
            PageChecksum.WriteChecksum(page, PageSize);
            _pages.Add(id, page);
            return ValueTask.FromResult(id);
        }
    }

    public ValueTask FreeAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_pages.Remove(pageId)) throw new StorageResourceException($"Cannot free unknown or already freed {pageId}.", new KeyNotFoundException());
            _free.Push(pageId);
            return ValueTask.CompletedTask;
        }
    }

    public ValueTask ReadAsync(PageId pageId, Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        ValidateBuffer(destination.Length, nameof(destination));
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_pages.TryGetValue(pageId, out var page)) throw new StorageResourceException($"Unknown or freed {pageId}.", new KeyNotFoundException());
            page.CopyTo(destination.Span);
            return ValueTask.CompletedTask;
        }
    }

    public ValueTask WriteAsync(PageId pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        ValidateBuffer(source.Length, nameof(source));
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_pages.TryGetValue(pageId, out var page)) throw new StorageResourceException($"Unknown or freed {pageId}.", new KeyNotFoundException());
            source.Span.CopyTo(page);
            return ValueTask.CompletedTask;
        }
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync) ThrowIfDisposed();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync) { _disposed = true; _pages.Clear(); _free.Clear(); }
        return ValueTask.CompletedTask;
    }

    private void ValidateBuffer(int length, string parameterName)
    {
        if (length != PageSize) throw new ArgumentException($"Buffer must be exactly {PageSize} bytes.", parameterName);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
