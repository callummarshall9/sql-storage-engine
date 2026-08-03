using System.Diagnostics;
using System.Runtime.CompilerServices;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Heap;

public enum TableHeapLookupResult
{
    Found,
    UnknownPage,
    UnknownSlot,
    Deleted,
    StaleGeneration
}

/// <summary>Stores raw rows across a linked chain of pinned heap pages.</summary>
public sealed class TableHeap
{
    private readonly BufferPool _bufferPool;
    private readonly IPageAllocator _allocator;
    private readonly IFreeSpaceMap _freeSpaceMap;
    private readonly HashSet<PageId> _knownPages = [];

    public TableHeap(PageId rootPageId, BufferPool bufferPool, IPageAllocator allocator,
        IFreeSpaceMap? freeSpaceMap = null)
    {
        ArgumentNullException.ThrowIfNull(bufferPool);
        ArgumentNullException.ThrowIfNull(allocator);
        RootPageId = rootPageId;
        _bufferPool = bufferPool;
        _allocator = allocator;
        _freeSpaceMap = freeSpaceMap ?? new InMemoryFreeSpaceMap(bufferPool.PageSize);
        _knownPages.Add(rootPageId);
    }

    public PageId RootPageId { get; }
    public IFreeSpaceMap FreeSpaceMap => _freeSpaceMap;

    /// <summary>Allocates and initializes the first page of a table heap.</summary>
    public static async ValueTask<TableHeap> CreateAsync(BufferPool bufferPool, IPageAllocator allocator,
        IFreeSpaceMap? freeSpaceMap = null,
        CancellationToken cancellationToken = default)
    {
        var root = await allocator.AllocateAsync(PageType.Heap, cancellationToken).ConfigureAwait(false);
        using var pin = await bufferPool.GetPageAsync(root, cancellationToken).ConfigureAwait(false);
        HeapPageLayout.Initialize(pin.Memory.Span, root);
        var page = new HeapPage(pin.Memory, root);
        pin.MarkDirty(new LogSequenceNumber(0));
        var table = new TableHeap(root, bufferPool, allocator, freeSpaceMap);
        table._freeSpaceMap.Update(root, page.FreeBytes);
        return table;
    }

    /// <summary>Opens an existing heap and rebuilds its volatile free-space map from page headers.</summary>
    public static async ValueTask<TableHeap> OpenAsync(PageId rootPageId, BufferPool bufferPool,
        IPageAllocator allocator, IFreeSpaceMap? freeSpaceMap = null,
        CancellationToken cancellationToken = default)
    {
        var table = new TableHeap(rootPageId, bufferPool, allocator, freeSpaceMap);
        await table.RebuildFreeSpaceMapAsync(cancellationToken).ConfigureAwait(false);
        return table;
    }

    /// <summary>Inserts into the first fitting page or appends a newly allocated heap page.</summary>
    public async ValueTask<RowId> InsertAsync(ReadOnlyMemory<byte> row, CancellationToken cancellationToken = default)
    {
        if (row.IsEmpty) throw new ArgumentException("Raw heap records cannot be empty.", nameof(row));
        if (row.Length > _bufferPool.PageSize - HeapPageLayout.HeaderLength - HeapPageLayout.SlotEntryLength)
            throw new StorageResourceExhaustedException("Row is too large for an empty heap page.");
        var requiredBytes = checked(row.Length + HeapPageLayout.SlotEntryLength);
        while (_freeSpaceMap.FindPage(requiredBytes) is { } candidate)
        {
            if (!_knownPages.Contains(candidate))
            {
                _freeSpaceMap.Remove(candidate);
                continue;
            }
            using var candidatePin = await _bufferPool.GetPageAsync(candidate, cancellationToken).ConfigureAwait(false);
            var candidatePage = new HeapPage(candidatePin.Memory, candidate);
            if (candidatePage.TryInsert(row.Span, out var candidateSlot, out var candidateGeneration))
            {
                _freeSpaceMap.Update(candidate, candidatePage.FreeBytes);
                candidatePin.MarkDirty(new LogSequenceNumber(0));
                return new RowId(candidate, candidateSlot, candidateGeneration);
            }
            _freeSpaceMap.Update(candidate, candidatePage.FreeBytes);
            _freeSpaceMap.Remove(candidate);
        }
        var current = RootPageId;
        while (true)
        {
            PageId? next;
            using (var pin = await _bufferPool.GetPageAsync(current, cancellationToken).ConfigureAwait(false))
            {
                var page = new HeapPage(pin.Memory, current);
                _knownPages.Add(current);
                if (page.TryInsert(row.Span, out var slotId, out var generation))
                {
                    _freeSpaceMap.Update(current, page.FreeBytes);
                    pin.MarkDirty(new LogSequenceNumber(0));
                    return new RowId(current, slotId, generation);
                }
                _freeSpaceMap.Update(current, page.FreeBytes);
                next = page.NextPageId;
            }
            if (next is { } nextPage)
            {
                current = nextPage;
                continue;
            }

            var allocated = await _allocator.AllocateAsync(PageType.Heap, cancellationToken).ConfigureAwait(false);
            SlotId insertedSlot;
            SlotGeneration insertedGeneration;
            using (var newPin = await _bufferPool.GetPageAsync(allocated, cancellationToken).ConfigureAwait(false))
            {
                HeapPageLayout.Initialize(newPin.Memory.Span, allocated, current);
                var newPage = new HeapPage(newPin.Memory, allocated);
                if (!newPage.TryInsert(row.Span, out insertedSlot, out insertedGeneration))
                    throw new StorageResourceExhaustedException("Row is too large for an empty heap page.");
                newPin.MarkDirty(new LogSequenceNumber(0));
                _freeSpaceMap.Update(allocated, newPage.FreeBytes);
                _knownPages.Add(allocated);
            }
            using var tailPin = await _bufferPool.GetPageAsync(current, cancellationToken).ConfigureAwait(false);
            var tail = new HeapPage(tailPin.Memory, current);
            tail.SetNextPage(allocated);
            tailPin.MarkDirty(new LogSequenceNumber(0));
            return new RowId(allocated, insertedSlot, insertedGeneration);
        }
    }

    public async ValueTask<bool> DeleteAsync(RowId rowId, CancellationToken cancellationToken = default)
    {
        var located = await FindPageAsync(rowId.PageId, cancellationToken).ConfigureAwait(false);
        if (!located) return false;
        using var pin = await _bufferPool.GetPageAsync(rowId.PageId, cancellationToken).ConfigureAwait(false);
        var page = new HeapPage(pin.Memory, rowId.PageId);
        if (!page.Delete(rowId.SlotId, rowId.Generation)) return false;
        _freeSpaceMap.Update(rowId.PageId, page.FreeBytes);
        pin.MarkDirty(new LogSequenceNumber(0));
        return true;
    }

    public async ValueTask<HeapUpdateResult> UpdateAsync(RowId rowId, ReadOnlyMemory<byte> row,
        CancellationToken cancellationToken = default)
    {
        if (!await FindPageAsync(rowId.PageId, cancellationToken).ConfigureAwait(false)) return HeapUpdateResult.Absent;
        using var pin = await _bufferPool.GetPageAsync(rowId.PageId, cancellationToken).ConfigureAwait(false);
        var page = new HeapPage(pin.Memory, rowId.PageId);
        var result = page.Update(rowId.SlotId, rowId.Generation, row.Span);
        _freeSpaceMap.Update(rowId.PageId, page.FreeBytes);
        if (result == HeapUpdateResult.Updated) pin.MarkDirty(new LogSequenceNumber(0));
        return result;
    }

    public async ValueTask<bool> CompactPageAsync(PageId pageId, CancellationToken cancellationToken = default)
        => (await CompactPageWithResultAsync(pageId, cancellationToken).ConfigureAwait(false)).Compacted;

    public async ValueTask<(bool Compacted, int ReclaimedBytes)> CompactPageWithResultAsync(PageId pageId,
        CancellationToken cancellationToken = default)
    {
        if (!await FindPageAsync(pageId, cancellationToken).ConfigureAwait(false)) return (false, 0);
        using var pin = await _bufferPool.GetPageAsync(pageId, cancellationToken).ConfigureAwait(false);
        var page = new HeapPage(pin.Memory, pageId);
        var before = page.FreeBytes;
        page.Compact();
        _freeSpaceMap.Update(pageId, page.FreeBytes);
        pin.MarkDirty(new LogSequenceNumber(0));
        return (true, page.FreeBytes - before);
    }

    /// <summary>Returns a bounded snapshot of the validated heap chain for incremental maintenance.</summary>
    public async ValueTask<IReadOnlyList<PageId>> GetPageIdsAsync(int maximumPages,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPages);
        var pages = new List<PageId>();
        var current = RootPageId;
        var seen = new HashSet<PageId>();
        while (seen.Add(current))
        {
            if (pages.Count == maximumPages) throw new StorageResourceExhaustedException("Heap maintenance scan-page limit exceeded.");
            pages.Add(current);
            using var pin = await _bufferPool.GetPageAsync(current, cancellationToken).ConfigureAwait(false);
            var page = new HeapPage(pin.Memory, current);
            if (page.NextPageId is not { } next) return pages.AsReadOnly();
            current = next;
        }
        throw new StorageCorruptionException($"Cycle detected in table heap at {current}.");
    }

    /// <summary>Reconstructs volatile free-space hints from the validated persisted page chain.</summary>
    public async ValueTask RebuildFreeSpaceMapAsync(CancellationToken cancellationToken = default)
    {
        _freeSpaceMap.Clear();
        _knownPages.Clear();
        var current = RootPageId;
        while (_knownPages.Add(current))
        {
            using var pin = await _bufferPool.GetPageAsync(current, cancellationToken).ConfigureAwait(false);
            var page = new HeapPage(pin.Memory, current);
            _freeSpaceMap.Update(current, page.FreeBytes);
            if (page.NextPageId is not { } next) return;
            current = next;
        }
        throw new StorageCorruptionException($"Cycle detected while rebuilding free-space map at {current}.");
    }

    /// <summary>Looks up a row only when its page belongs to this table's validated chain.</summary>
    public async ValueTask<(TableHeapLookupResult Result, ReadOnlyMemory<byte> Row)> ReadAsync(
        RowId rowId, CancellationToken cancellationToken = default)
    {
        var current = RootPageId;
        HashSet<PageId> seen = [];
        while (seen.Add(current))
        {
            using var pin = await _bufferPool.GetPageAsync(current, cancellationToken).ConfigureAwait(false);
            var page = new HeapPage(pin.Memory, current);
            if (current == rowId.PageId)
            {
                var result = page.Read(rowId.SlotId, rowId.Generation, out var row);
                return (result switch
                {
                    HeapReadResult.Found => TableHeapLookupResult.Found,
                    HeapReadResult.UnknownSlot => TableHeapLookupResult.UnknownSlot,
                    HeapReadResult.Deleted => TableHeapLookupResult.Deleted,
                    HeapReadResult.StaleGeneration => TableHeapLookupResult.StaleGeneration,
                    _ => throw new UnreachableException()
                }, row);
            }
            if (page.NextPageId is not { } next) return (TableHeapLookupResult.UnknownPage, default);
            current = next;
        }
        throw new StorageCorruptionException($"Cycle detected in table heap at {current}.");
    }

    /// <summary>Enumerates live rows once in page-chain and slot order.</summary>
    public async IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> ScanAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var current = RootPageId;
        HashSet<PageId> seen = [];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(current)) throw new StorageCorruptionException($"Cycle detected in table heap at {current}.");
            IReadOnlyList<HeapPageRow> rows;
            PageId? next;
            try
            {
                using var pin = await _bufferPool.GetPageAsync(current, cancellationToken).ConfigureAwait(false);
                var page = new HeapPage(pin.Memory, current);
                rows = page.ReadLiveRows();
                next = page.NextPageId;
            }
            catch (StorageResourceException exception)
            {
                throw new StorageCorruptionException($"Heap chain references inaccessible {current}.", exception);
            }

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return (new RowId(current, row.SlotId, row.Generation), row.Bytes);
            }
            if (next is not { } nextPage) yield break;
            current = nextPage;
        }
    }

    private async ValueTask<bool> FindPageAsync(PageId target, CancellationToken cancellationToken)
    {
        if (_knownPages.Contains(target)) return true;
        var current = RootPageId;
        HashSet<PageId> seen = [];
        while (seen.Add(current))
        {
            _knownPages.Add(current);
            if (current == target) return true;
            using var pin = await _bufferPool.GetPageAsync(current, cancellationToken).ConfigureAwait(false);
            var page = new HeapPage(pin.Memory, current);
            if (page.NextPageId is not { } next) return false;
            current = next;
        }
        throw new StorageCorruptionException($"Cycle detected in table heap at {current}.");
    }
}
