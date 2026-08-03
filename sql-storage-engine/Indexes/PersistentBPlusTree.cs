using System.Runtime.CompilerServices;
using sql_storage_engine.Buffers;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Indexes;

public enum IndexInsertResult
{
    Inserted,
    SplitRequired
}

public interface IIndexRootReference
{
    PageId RootPageId { get; }
    ValueTask UpdateRootAsync(PageId rootPageId, CancellationToken cancellationToken = default);
}

public sealed class MutableIndexRootReference(PageId rootPageId) : IIndexRootReference
{
    public PageId RootPageId { get; private set; } = rootPageId;
    public ValueTask UpdateRootAsync(PageId rootPageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (rootPageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(rootPageId));
        RootPageId = rootPageId;
        return ValueTask.CompletedTask;
    }
}

public readonly record struct IndexRange(
    IndexKey LowerBound,
    IndexKey UpperBound,
    bool IncludeLowerBound = true,
    bool IncludeUpperBound = true,
    ScanDirection Direction = ScanDirection.Ascending);

/// <summary>Reads a persistent B+ tree through bounded, short-lived buffer pins.</summary>
public sealed class PersistentBPlusTree
{
    public const int MaximumTreeHeight = 64;
    public const int MaximumScanPages = 8192;
    private readonly BufferPool _bufferPool;
    private readonly IPageAllocator _allocator;
    private readonly IIndexRootReference _rootReference;

    public PersistentBPlusTree(BufferPool bufferPool, IPageAllocator allocator, IIndexRootReference rootReference)
    {
        ArgumentNullException.ThrowIfNull(bufferPool);
        ArgumentNullException.ThrowIfNull(allocator);
        ArgumentNullException.ThrowIfNull(rootReference);
        _bufferPool = bufferPool;
        _allocator = allocator;
        _rootReference = rootReference;
    }

    public PageId RootPageId => _rootReference.RootPageId;

    /// <summary>Removes one exact key/RowId pair and borrows from a lending leaf sibling when needed.</summary>
    public async ValueTask<bool> RemoveAsync(IndexKey key, RowId rowId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        PageId? current = await FindLeafAsync(key, equalRoutesLeft: true, cancellationToken).ConfigureAwait(false);
        HashSet<PageId> visited = [];
        while (current is { } leafId && visited.Add(leafId))
        {
            var leaf = await ReadLeafAsync(leafId, cancellationToken).ConfigureAwait(false);
            var entryIndex = leaf.Entries.ToList().FindIndex(entry => entry.Key.Equals(key) && entry.RowId == rowId);
            if (entryIndex >= 0)
            {
                var entries = leaf.Entries.ToList();
                var removedMinimum = entryIndex == 0;
                entries.RemoveAt(entryIndex);
                var updated = leaf with { Entries = entries.AsReadOnly() };
                if (leaf.ParentPageId is null)
                {
                    await WriteLeafAsync(updated, cancellationToken).ConfigureAwait(false);
                    return true;
                }
                if (entries.Count < 2)
                    updated = await BorrowLeafEntryAsync(updated, cancellationToken).ConfigureAwait(false);
                else
                    await WriteLeafAsync(updated, cancellationToken).ConfigureAwait(false);
                if (updated.Entries.Count > 0 && removedMinimum)
                    await RefreshAncestorMinimumAsync(updated.PageId, updated.Entries[0].Key, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (leaf.Entries.Count > 0 && leaf.Entries[^1].Key.CompareTo(key) > 0) return false;
            current = leaf.NextPageId;
        }
        if (current is not null) throw new StorageCorruptionException("Cycle detected while locating duplicate index entries.");
        return false;
    }

    /// <summary>Inserts into a leaf only when it already has capacity.</summary>
    public async ValueTask<IndexInsertResult> InsertWithoutSplitAsync(IndexKey key, RowId rowId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (rowId.PageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(rowId));
        var leafId = await FindLeafAsync(key, equalRoutesLeft: false, cancellationToken).ConfigureAwait(false);
        IndexKey? oldMinimum;
        using (var pin = await GetPinAsync(leafId, cancellationToken).ConfigureAwait(false))
        {
            var leaf = LeafIndexPageCodec.Read(pin.Memory.Span, leafId);
            oldMinimum = leaf.Entries.Count == 0 ? null : leaf.Entries[0].Key;
            var entries = leaf.Entries.ToList();
            var insertAt = 0;
            while (insertAt < entries.Count && entries[insertAt].Key.CompareTo(key) <= 0) insertAt++;
            entries.Insert(insertAt, new LeafIndexEntry(key, rowId));
            if (!LeafIndexPageCodec.CanFit(_bufferPool.PageSize, entries)) return IndexInsertResult.SplitRequired;
            LeafIndexPageCodec.Write(pin.Memory.Span, leaf with { Entries = entries.AsReadOnly() });
            pin.MarkDirty(new LogSequenceNumber(0));
        }
        if (oldMinimum is null || key.CompareTo(oldMinimum) < 0)
            await RefreshAncestorMinimumAsync(leafId, key, cancellationToken).ConfigureAwait(false);
        return IndexInsertResult.Inserted;
    }

    /// <summary>Inserts an entry and splits a full leaf, including root-leaf growth.</summary>
    public async ValueTask InsertAsync(IndexKey key, RowId rowId, CancellationToken cancellationToken = default)
    {
        if (await InsertWithoutSplitAsync(key, rowId, cancellationToken).ConfigureAwait(false) == IndexInsertResult.Inserted) return;
        var leafId = await FindLeafAsync(key, equalRoutesLeft: false, cancellationToken).ConfigureAwait(false);
        var leaf = await ReadLeafAsync(leafId, cancellationToken).ConfigureAwait(false);
        var entries = leaf.Entries.ToList();
        var insertAt = 0;
        while (insertAt < entries.Count && entries[insertAt].Key.CompareTo(key) <= 0) insertAt++;
        entries.Insert(insertAt, new LeafIndexEntry(key, rowId));
        var splitAt = entries.Count / 2;
        var leftEntries = entries[..splitAt];
        var rightEntries = entries[splitAt..];
        if (!LeafIndexPageCodec.CanFit(_bufferPool.PageSize, leftEntries) ||
            !LeafIndexPageCodec.CanFit(_bufferPool.PageSize, rightEntries))
            throw new StorageResourceExhaustedException("Index key is too large to split into valid leaf pages.");

        var rightId = await _allocator.AllocateAsync(PageType.BPlusTreeLeaf, cancellationToken).ConfigureAwait(false);
        await WriteLeafAsync(new LeafIndexPage(rightId, leaf.ParentPageId, leafId, leaf.NextPageId,
            rightEntries.AsReadOnly()), cancellationToken).ConfigureAwait(false);
        await WriteLeafAsync(leaf with { NextPageId = rightId,
            Entries = leftEntries.AsReadOnly() }, cancellationToken).ConfigureAwait(false);
        if (leaf.NextPageId is { } oldNext)
        {
            var neighbor = await ReadLeafAsync(oldNext, cancellationToken).ConfigureAwait(false);
            if (neighbor.PreviousPageId != leafId)
                throw new StorageCorruptionException("Forward and reverse leaf links disagree before split.");
            await WriteLeafAsync(neighbor with { PreviousPageId = rightId }, cancellationToken).ConfigureAwait(false);
        }

        await InsertChildIntoParentAsync(leafId, rightId, leftEntries[0].Key, rightEntries[0].Key,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<RowId>> FindAsync(IndexKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        PageId? leafId = await FindLeafAsync(key, equalRoutesLeft: true, cancellationToken).ConfigureAwait(false);
        List<RowId> result = [];
        HashSet<PageId> visited = [];
        for (var pages = 0; pages < MaximumScanPages && leafId is { } current; pages++)
        {
            if (!visited.Add(current)) throw new StorageCorruptionException($"Cycle detected in leaf chain at {current}.");
            var leaf = await ReadLeafAsync(current, cancellationToken).ConfigureAwait(false);
            var sawGreater = false;
            foreach (var entry in leaf.Entries)
            {
                var comparison = entry.Key.CompareTo(key);
                if (comparison == 0) result.Add(entry.RowId);
                if (comparison > 0) { sawGreater = true; break; }
            }
            if (sawGreater || leaf.Entries.Count > 0 && leaf.Entries[^1].Key.CompareTo(key) > 0) return result.AsReadOnly();
            leafId = leaf.NextPageId;
        }
        if (leafId is not null) throw new StorageCorruptionException("Exact lookup exceeded the leaf traversal bound.");
        return result.AsReadOnly();
    }

    public async IAsyncEnumerable<LeafIndexEntry> ScanAsync(IndexRange range,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (range.LowerBound.CompareTo(range.UpperBound) > 0) yield break;
        var current = range.Direction == ScanDirection.Ascending
            ? await FindEdgeLeafAsync(leftmost: true, cancellationToken).ConfigureAwait(false)
            : await FindEdgeLeafAsync(leftmost: false, cancellationToken).ConfigureAwait(false);
        HashSet<PageId> visited = [];
        for (var pages = 0; pages < MaximumScanPages && current is { } pageId; pages++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(pageId)) throw new StorageCorruptionException($"Cycle detected in leaf scan at {pageId}.");
            var leaf = await ReadLeafAsync(pageId, cancellationToken).ConfigureAwait(false);
            var entries = range.Direction == ScanDirection.Ascending
                ? leaf.Entries
                : leaf.Entries.Reverse().ToArray();
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lower = entry.Key.CompareTo(range.LowerBound);
                var upper = entry.Key.CompareTo(range.UpperBound);
                if ((lower > 0 || lower == 0 && range.IncludeLowerBound) &&
                    (upper < 0 || upper == 0 && range.IncludeUpperBound)) yield return entry;
            }
            current = range.Direction == ScanDirection.Ascending ? leaf.NextPageId : leaf.PreviousPageId;
        }
        if (current is not null) throw new StorageCorruptionException("Range scan exceeded the leaf traversal bound.");
    }

    internal async ValueTask<PageId> FindLeafAsync(IndexKey key, bool equalRoutesLeft,
        CancellationToken cancellationToken)
    {
        var current = RootPageId;
        HashSet<PageId> visited = [];
        for (var height = 0; height < MaximumTreeHeight; height++)
        {
            if (!visited.Add(current)) throw new StorageCorruptionException($"Cycle detected in index tree at {current}.");
            using var pin = await GetPinAsync(current, cancellationToken).ConfigureAwait(false);
            var type = PageHeaderCodec.Read(pin.Memory.Span).PageType;
            if (type == PageType.BPlusTreeLeaf)
            {
                _ = LeafIndexPageCodec.Read(pin.Memory.Span, current);
                return current;
            }
            if (type != PageType.BPlusTreeInternal)
                throw new StorageFormatException($"Expected index page, found {type} at {current}.");
            var node = InternalIndexPageCodec.Read(pin.Memory.Span, current);
            var childIndex = 0;
            while (childIndex < node.Separators.Count &&
                   (equalRoutesLeft ? node.Separators[childIndex].CompareTo(key) < 0 : node.Separators[childIndex].CompareTo(key) <= 0))
                childIndex++;
            current = node.Children[childIndex];
        }
        throw new StorageCorruptionException($"Index tree exceeds maximum height {MaximumTreeHeight}.");
    }

    private async ValueTask<PageId?> FindEdgeLeafAsync(bool leftmost, CancellationToken cancellationToken)
    {
        var current = RootPageId;
        HashSet<PageId> visited = [];
        for (var height = 0; height < MaximumTreeHeight; height++)
        {
            if (!visited.Add(current)) throw new StorageCorruptionException($"Cycle detected in index tree at {current}.");
            using var pin = await GetPinAsync(current, cancellationToken).ConfigureAwait(false);
            var type = PageHeaderCodec.Read(pin.Memory.Span).PageType;
            if (type == PageType.BPlusTreeLeaf)
            {
                _ = LeafIndexPageCodec.Read(pin.Memory.Span, current);
                return current;
            }
            if (type != PageType.BPlusTreeInternal)
                throw new StorageFormatException($"Expected index page, found {type} at {current}.");
            var node = InternalIndexPageCodec.Read(pin.Memory.Span, current);
            current = leftmost ? node.Children[0] : node.Children[^1];
        }
        throw new StorageCorruptionException($"Index tree exceeds maximum height {MaximumTreeHeight}.");
    }

    internal async ValueTask<LeafIndexPage> ReadLeafAsync(PageId pageId, CancellationToken cancellationToken)
    {
        using var pin = await GetPinAsync(pageId, cancellationToken).ConfigureAwait(false);
        return LeafIndexPageCodec.Read(pin.Memory.Span, pageId);
    }

    internal async ValueTask<IPinnedPage> GetPinAsync(PageId pageId, CancellationToken cancellationToken)
    {
        try { return await _bufferPool.GetPageAsync(pageId, cancellationToken).ConfigureAwait(false); }
        catch (StorageResourceException exception)
        {
            throw new StorageCorruptionException($"Index references inaccessible {pageId}.", exception);
        }
    }

    private async ValueTask WriteLeafAsync(LeafIndexPage leaf, CancellationToken cancellationToken)
    {
        using var pin = await GetPinAsync(leaf.PageId, cancellationToken).ConfigureAwait(false);
        LeafIndexPageCodec.Write(pin.Memory.Span, leaf);
        pin.MarkDirty(new LogSequenceNumber(0));
    }

    private async ValueTask WriteInternalAsync(InternalIndexPage node, CancellationToken cancellationToken)
    {
        using var pin = await GetPinAsync(node.PageId, cancellationToken).ConfigureAwait(false);
        InternalIndexPageCodec.Write(pin.Memory.Span, node);
        pin.MarkDirty(new LogSequenceNumber(0));
    }

    private async ValueTask InsertChildIntoParentAsync(PageId leftId, PageId rightId, IndexKey leftMinimum,
        IndexKey rightMinimum, CancellationToken cancellationToken)
    {
        var parentId = await ReadParentIdAsync(leftId, cancellationToken).ConfigureAwait(false);
        if (parentId is null)
        {
            var rootId = await _allocator.AllocateAsync(PageType.BPlusTreeInternal, cancellationToken).ConfigureAwait(false);
            await WriteInternalAsync(new InternalIndexPage(rootId, null, new[] { rightMinimum },
                new[] { leftId, rightId }), cancellationToken).ConfigureAwait(false);
            await SetParentAsync(leftId, rootId, cancellationToken).ConfigureAwait(false);
            await SetParentAsync(rightId, rootId, cancellationToken).ConfigureAwait(false);
            await _rootReference.UpdateRootAsync(rootId, cancellationToken).ConfigureAwait(false);
            return;
        }

        InternalIndexPage parent;
        using (var pin = await GetPinAsync(parentId.Value, cancellationToken).ConfigureAwait(false))
            parent = InternalIndexPageCodec.Read(pin.Memory.Span, parentId.Value);
        var childIndex = parent.Children.ToList().IndexOf(leftId);
        if (childIndex < 0) throw new StorageCorruptionException($"Parent {parent.PageId} does not reference child {leftId}.");
        var separators = parent.Separators.ToList();
        var children = parent.Children.ToList();
        separators.Insert(childIndex, rightMinimum);
        children.Insert(childIndex + 1, rightId);
        if (childIndex > 0) separators[childIndex - 1] = leftMinimum;

        if (InternalIndexPageCodec.CanFit(_bufferPool.PageSize, separators))
        {
            await WriteInternalAsync(parent with { Separators = separators.AsReadOnly(), Children = children.AsReadOnly() },
                cancellationToken).ConfigureAwait(false);
            await SetParentAsync(rightId, parent.PageId, cancellationToken).ConfigureAwait(false);
            if (childIndex == 0) await RefreshAncestorMinimumAsync(parent.PageId, leftMinimum, cancellationToken).ConfigureAwait(false);
            return;
        }

        var splitChildIndex = ChooseInternalSplit(children, separators);
        var promoted = separators[splitChildIndex - 1];
        var leftChildren = children[..splitChildIndex];
        var rightChildren = children[splitChildIndex..];
        var leftSeparators = separators[..(splitChildIndex - 1)];
        var rightSeparators = separators[splitChildIndex..];
        var newRightId = await _allocator.AllocateAsync(PageType.BPlusTreeInternal, cancellationToken).ConfigureAwait(false);
        await WriteInternalAsync(parent with { Separators = leftSeparators.AsReadOnly(), Children = leftChildren.AsReadOnly() },
            cancellationToken).ConfigureAwait(false);
        await WriteInternalAsync(new InternalIndexPage(newRightId, parent.ParentPageId,
            rightSeparators.AsReadOnly(), rightChildren.AsReadOnly()), cancellationToken).ConfigureAwait(false);
        foreach (var movedChild in rightChildren)
            await SetParentAsync(movedChild, newRightId, cancellationToken).ConfigureAwait(false);
        await InsertChildIntoParentAsync(parent.PageId, newRightId, leftMinimum, promoted, cancellationToken)
            .ConfigureAwait(false);
    }

    private int ChooseInternalSplit(IReadOnlyList<PageId> children, IReadOnlyList<IndexKey> separators)
    {
        var preferred = children.Count / 2;
        return Enumerable.Range(2, children.Count - 3)
            .OrderBy(index => Math.Abs(index - preferred))
            .FirstOrDefault(index =>
                InternalIndexPageCodec.CanFit(_bufferPool.PageSize, separators.Take(index - 1)) &&
                InternalIndexPageCodec.CanFit(_bufferPool.PageSize, separators.Skip(index))) switch
        {
            0 => throw new StorageResourceExhaustedException("Internal entries cannot be redistributed into two valid pages."),
            var index => index
        };
    }

    private async ValueTask<PageId?> ReadParentIdAsync(PageId pageId, CancellationToken cancellationToken)
    {
        using var pin = await GetPinAsync(pageId, cancellationToken).ConfigureAwait(false);
        return PageHeaderCodec.Read(pin.Memory.Span).PageType switch
        {
            PageType.BPlusTreeLeaf => LeafIndexPageCodec.Read(pin.Memory.Span, pageId).ParentPageId,
            PageType.BPlusTreeInternal => InternalIndexPageCodec.Read(pin.Memory.Span, pageId).ParentPageId,
            var type => throw new StorageFormatException($"Expected index child, found {type}.")
        };
    }

    private async ValueTask SetParentAsync(PageId pageId, PageId? parentId, CancellationToken cancellationToken)
    {
        using var pin = await GetPinAsync(pageId, cancellationToken).ConfigureAwait(false);
        switch (PageHeaderCodec.Read(pin.Memory.Span).PageType)
        {
            case PageType.BPlusTreeLeaf:
                var leaf = LeafIndexPageCodec.Read(pin.Memory.Span, pageId);
                LeafIndexPageCodec.Write(pin.Memory.Span, leaf with { ParentPageId = parentId });
                break;
            case PageType.BPlusTreeInternal:
                var node = InternalIndexPageCodec.Read(pin.Memory.Span, pageId);
                InternalIndexPageCodec.Write(pin.Memory.Span, node with { ParentPageId = parentId });
                break;
            default:
                throw new StorageFormatException("Cannot assign a parent to a non-index page.");
        }
        pin.MarkDirty(new LogSequenceNumber(0));
    }

    private async ValueTask<LeafIndexPage> BorrowLeafEntryAsync(LeafIndexPage leaf,
        CancellationToken cancellationToken)
    {
        if (leaf.PreviousPageId is { } previousId)
        {
            var left = await ReadLeafAsync(previousId, cancellationToken).ConfigureAwait(false);
            if (left.ParentPageId == leaf.ParentPageId && left.Entries.Count > 2)
            {
                var leftEntries = left.Entries.ToList();
                var leafEntries = leaf.Entries.ToList();
                leafEntries.Insert(0, leftEntries[^1]);
                leftEntries.RemoveAt(leftEntries.Count - 1);
                var updatedLeft = left with { Entries = leftEntries.AsReadOnly() };
                var updatedLeaf = leaf with { Entries = leafEntries.AsReadOnly() };
                await WriteLeafAsync(updatedLeft, cancellationToken).ConfigureAwait(false);
                await WriteLeafAsync(updatedLeaf, cancellationToken).ConfigureAwait(false);
                await RefreshAncestorMinimumAsync(updatedLeaf.PageId, updatedLeaf.Entries[0].Key, cancellationToken)
                    .ConfigureAwait(false);
                return updatedLeaf;
            }
        }
        if (leaf.NextPageId is { } nextId)
        {
            var right = await ReadLeafAsync(nextId, cancellationToken).ConfigureAwait(false);
            if (right.ParentPageId == leaf.ParentPageId && right.Entries.Count > 2)
            {
                var rightEntries = right.Entries.ToList();
                var leafEntries = leaf.Entries.ToList();
                leafEntries.Add(rightEntries[0]);
                rightEntries.RemoveAt(0);
                var updatedLeaf = leaf with { Entries = leafEntries.AsReadOnly() };
                var updatedRight = right with { Entries = rightEntries.AsReadOnly() };
                await WriteLeafAsync(updatedLeaf, cancellationToken).ConfigureAwait(false);
                await WriteLeafAsync(updatedRight, cancellationToken).ConfigureAwait(false);
                await RefreshAncestorMinimumAsync(updatedRight.PageId, updatedRight.Entries[0].Key, cancellationToken)
                    .ConfigureAwait(false);
                return updatedLeaf;
            }
        }
        await WriteLeafAsync(leaf, cancellationToken).ConfigureAwait(false);
        return leaf;
    }

    private async ValueTask RefreshAncestorMinimumAsync(PageId childId, IndexKey minimum,
        CancellationToken cancellationToken)
    {
        PageId? parentId;
        using (var childPin = await GetPinAsync(childId, cancellationToken).ConfigureAwait(false))
        {
            var type = PageHeaderCodec.Read(childPin.Memory.Span).PageType;
            parentId = type switch
            {
                PageType.BPlusTreeLeaf => LeafIndexPageCodec.Read(childPin.Memory.Span, childId).ParentPageId,
                PageType.BPlusTreeInternal => InternalIndexPageCodec.Read(childPin.Memory.Span, childId).ParentPageId,
                _ => throw new StorageFormatException("Index ancestor has the wrong page type.")
            };
        }
        if (parentId is not { } parent) return;
        var propagate = false;
        using (var parentPin = await GetPinAsync(parent, cancellationToken).ConfigureAwait(false))
        {
            var node = InternalIndexPageCodec.Read(parentPin.Memory.Span, parent);
            var childIndex = node.Children.ToList().IndexOf(childId);
            if (childIndex < 0) throw new StorageCorruptionException($"Parent {parent} does not reference child {childId}.");
            if (childIndex == 0) propagate = true;
            else
            {
                var separators = node.Separators.ToList();
                separators[childIndex - 1] = minimum;
                InternalIndexPageCodec.Write(parentPin.Memory.Span, node with { Separators = separators.AsReadOnly() });
                parentPin.MarkDirty(new LogSequenceNumber(0));
            }
        }
        if (propagate) await RefreshAncestorMinimumAsync(parent, minimum, cancellationToken).ConfigureAwait(false);
    }
}
