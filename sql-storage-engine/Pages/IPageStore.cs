using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Pages;

/// <summary>Provides complete, offset-independent page I/O. Caller buffers are retained only until completion.</summary>
public interface IPageStore : IAsyncDisposable
{
    /// <summary>Gets the exact byte length required for every read and write buffer.</summary>
    int PageSize { get; }
    /// <summary>Reads one complete page. Cancellation may abort blocking backing-store I/O.</summary>
    ValueTask ReadAsync(PageId pageId, Memory<byte> destination, CancellationToken cancellationToken = default);
    /// <summary>Writes one complete page without retaining <paramref name="source"/> after completion.</summary>
    ValueTask WriteAsync(PageId pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default);
    /// <summary>Makes preceding writes durable according to the backing store's platform contract.</summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>Owns page allocation metadata separately from raw page I/O.</summary>
public interface IPageAllocator
{
    /// <summary>Allocates a uniquely owned page, preferring persisted free pages.</summary>
    ValueTask<PageId> AllocateAsync(PageType pageType, CancellationToken cancellationToken = default);
    /// <summary>Returns a live non-header page to the free list.</summary>
    ValueTask FreeAsync(PageId pageId, CancellationToken cancellationToken = default);
}
