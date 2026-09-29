using sql_storage_engine.Identifiers;

namespace sql_storage_engine.Pages;

/// <summary>Owns page allocation metadata separately from raw page I/O.</summary>
public interface IPageAllocator
{
    /// <summary>Allocates a uniquely owned page, preferring persisted free pages.</summary>
    ValueTask<PageId> AllocateAsync(PageType pageType, CancellationToken cancellationToken = default);
    /// <summary>Returns a live non-header page to the free list.</summary>
    ValueTask FreeAsync(PageId pageId, CancellationToken cancellationToken = default);
}
