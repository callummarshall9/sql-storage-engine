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

/// <summary>Describes an exact index deletion and pages that may be reclaimed after it is safe to do so.</summary>
public sealed record IndexDeleteResult(bool Removed, IReadOnlyList<PageId> RetiredPageIds);

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
    private readonly bool _isUnique;

    public PersistentBPlusTree(BufferPool bufferPool, IPageAllocator allocator, IIndexRootReference rootReference,
        bool isUnique = false)
    {
        ArgumentNullException.ThrowIfNull(bufferPool);
        ArgumentNullException.ThrowIfNull(allocator);
        ArgumentNullException.ThrowIfNull(rootReference);
        _bufferPool = bufferPool;
        _allocator = allocator;
        _rootReference = rootReference;
        _isUnique = isUnique;
    }

    public PageId RootPageId => _rootReference.RootPageId;
    public bool IsUnique => _isUnique;

    /// <summary>Removes one exact key/RowId pair.</summary>
    public async ValueTask<bool> RemoveAsync(IndexKey key, RowId rowId, CancellationToken cancellationToken = default) =>
        (await DeleteAsync(key, rowId, cancellationToken).ConfigureAwait(false)).Removed;

    /// <summary>
    /// Removes one exact key/RowId pair and reports pages retired by merges. Reported pages remain allocated and must not be
    /// reused until a later transaction/reclamation layer establishes that no reader can reference them.
    /// </summary>
    public async ValueTask<IndexDeleteResult> DeleteAsync(IndexKey key, RowId rowId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        List<PageId> retiredPageIds = [];
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
                    return new IndexDeleteResult(true, retiredPageIds.AsReadOnly());
                }
                if (entries.Count < 2)
                    await RebalanceLeafAsync(updated, retiredPageIds, cancellationToken).ConfigureAwait(false);
                else
                {
                    await WriteLeafAsync(updated, cancellationToken).ConfigureAwait(false);
                    if (removedMinimum)
                        await RefreshAncestorMinimumAsync(updated.PageId, updated.Entries[0].Key, cancellationToken)
                            .ConfigureAwait(false);
                }
                return new IndexDeleteResult(true, retiredPageIds.AsReadOnly());
            }
            if (leaf.Entries.Count > 0 && leaf.Entries[^1].Key.CompareTo(key) > 0)
                return new IndexDeleteResult(false, retiredPageIds.AsReadOnly());
            current = leaf.NextPageId;
        }
        if (current is not null) throw new StorageCorruptionException("Cycle detected while locating duplicate index entries.");
        return new IndexDeleteResult(false, retiredPageIds.AsReadOnly());
    }

    /// <summary>Inserts into a leaf only when it already has capacity.</summary>
    public async ValueTask<IndexInsertResult> InsertWithoutSplitAsync(IndexKey key, RowId rowId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (rowId.PageId.Value == 0) throw new ArgumentOutOfRangeException(nameof(rowId));
        // This preflight establishes logical behavior only; transactional race protection belongs to the locking layer.
        if (_isUnique && (await FindAsync(key, cancellationToken).ConfigureAwait(false)).Count != 0)
            throw new DuplicateIndexKeyException();
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

    private async ValueTask RebalanceLeafAsync(LeafIndexPage leaf, List<PageId> retiredPageIds,
        CancellationToken cancellationToken)
    {
        if (leaf.ParentPageId is not { } parentId)
            throw new StorageCorruptionException("A non-root leaf has no parent.");
        var parent = await ReadInternalAsync(parentId, cancellationToken).ConfigureAwait(false);
        var childIndex = parent.Children.ToList().IndexOf(leaf.PageId);
        if (childIndex < 0) throw new StorageCorruptionException($"Parent {parentId} does not reference leaf {leaf.PageId}.");

        if (childIndex > 0)
        {
            var left = await ReadLeafAsync(parent.Children[childIndex - 1], cancellationToken).ConfigureAwait(false);
            if (left.Entries.Count > 2)
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
                return;
            }
        }
        if (childIndex + 1 < parent.Children.Count)
        {
            var right = await ReadLeafAsync(parent.Children[childIndex + 1], cancellationToken).ConfigureAwait(false);
            if (right.Entries.Count > 2)
            {
                var rightEntries = right.Entries.ToList();
                var leafEntries = leaf.Entries.ToList();
                leafEntries.Add(rightEntries[0]);
                rightEntries.RemoveAt(0);
                var updatedLeaf = leaf with { Entries = leafEntries.AsReadOnly() };
                var updatedRight = right with { Entries = rightEntries.AsReadOnly() };
                await WriteLeafAsync(updatedLeaf, cancellationToken).ConfigureAwait(false);
                await WriteLeafAsync(updatedRight, cancellationToken).ConfigureAwait(false);
                await RefreshAncestorMinimumAsync(updatedLeaf.PageId, updatedLeaf.Entries[0].Key, cancellationToken)
                    .ConfigureAwait(false);
                await RefreshAncestorMinimumAsync(updatedRight.PageId, updatedRight.Entries[0].Key, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        if (childIndex > 0)
        {
            var left = await ReadLeafAsync(parent.Children[childIndex - 1], cancellationToken).ConfigureAwait(false);
            var mergedEntries = left.Entries.Concat(leaf.Entries).ToArray();
            if (!LeafIndexPageCodec.CanFit(_bufferPool.PageSize, mergedEntries))
                throw new StorageResourceExhaustedException("Underfilled leaf siblings cannot be merged.");
            await WriteLeafAsync(left with { NextPageId = leaf.NextPageId, Entries = mergedEntries }, cancellationToken)
                .ConfigureAwait(false);
            if (leaf.NextPageId is { } nextId)
            {
                var next = await ReadLeafAsync(nextId, cancellationToken).ConfigureAwait(false);
                await WriteLeafAsync(next with { PreviousPageId = left.PageId }, cancellationToken).ConfigureAwait(false);
            }
            retiredPageIds.Add(leaf.PageId);
            await RemoveChildFromInternalAsync(parent, childIndex, retiredPageIds, cancellationToken).ConfigureAwait(false);
            return;
        }

        var rightSibling = await ReadLeafAsync(parent.Children[1], cancellationToken).ConfigureAwait(false);
        var rightMergedEntries = leaf.Entries.Concat(rightSibling.Entries).ToArray();
        if (!LeafIndexPageCodec.CanFit(_bufferPool.PageSize, rightMergedEntries))
            throw new StorageResourceExhaustedException("Underfilled leaf siblings cannot be merged.");
        await WriteLeafAsync(leaf with { NextPageId = rightSibling.NextPageId, Entries = rightMergedEntries }, cancellationToken)
            .ConfigureAwait(false);
        if (rightSibling.NextPageId is { } followingId)
        {
            var following = await ReadLeafAsync(followingId, cancellationToken).ConfigureAwait(false);
            await WriteLeafAsync(following with { PreviousPageId = leaf.PageId }, cancellationToken).ConfigureAwait(false);
        }
        retiredPageIds.Add(rightSibling.PageId);
        await RemoveChildFromInternalAsync(parent, 1, retiredPageIds, cancellationToken).ConfigureAwait(false);
        await RefreshAncestorMinimumAsync(leaf.PageId, rightMergedEntries[0].Key, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RemoveChildFromInternalAsync(InternalIndexPage node, int childIndex,
        List<PageId> retiredPageIds, CancellationToken cancellationToken)
    {
        var children = node.Children.ToList();
        var separators = node.Separators.ToList();
        children.RemoveAt(childIndex);
        separators.RemoveAt(childIndex == 0 ? 0 : childIndex - 1);
        if (children.Count >= 2)
        {
            await WriteInternalAsync(node with { Children = children.AsReadOnly(), Separators = separators.AsReadOnly() },
                cancellationToken).ConfigureAwait(false);
            return;
        }
        if (node.ParentPageId is null)
        {
            await SetParentAsync(children[0], null, cancellationToken).ConfigureAwait(false);
            await _rootReference.UpdateRootAsync(children[0], cancellationToken).ConfigureAwait(false);
            retiredPageIds.Add(node.PageId);
            return;
        }
        await RebalanceInternalAsync(node with { Children = children.AsReadOnly(), Separators = separators.AsReadOnly() },
            retiredPageIds, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask RebalanceInternalAsync(InternalIndexPage node, List<PageId> retiredPageIds,
        CancellationToken cancellationToken)
    {
        var grandparent = await ReadInternalAsync(node.ParentPageId!.Value, cancellationToken).ConfigureAwait(false);
        var nodeIndex = grandparent.Children.ToList().IndexOf(node.PageId);
        if (nodeIndex < 0) throw new StorageCorruptionException($"Parent {grandparent.PageId} does not reference {node.PageId}.");

        if (nodeIndex > 0)
        {
            var left = await ReadInternalAsync(grandparent.Children[nodeIndex - 1], cancellationToken).ConfigureAwait(false);
            if (left.Children.Count > 2)
            {
                var moved = left.Children[^1];
                var leftChildren = left.Children.Take(left.Children.Count - 1).ToArray();
                var leftSeparators = left.Separators.Take(left.Separators.Count - 1).ToArray();
                var nodeChildren = node.Children.Prepend(moved).ToArray();
                var oldMinimum = await GetSubtreeMinimumAsync(node.Children[0], cancellationToken).ConfigureAwait(false);
                var nodeSeparators = node.Separators.Prepend(oldMinimum).ToArray();
                await WriteInternalAsync(left with { Children = leftChildren, Separators = leftSeparators }, cancellationToken)
                    .ConfigureAwait(false);
                await WriteInternalAsync(node with { Children = nodeChildren, Separators = nodeSeparators }, cancellationToken)
                    .ConfigureAwait(false);
                await SetParentAsync(moved, node.PageId, cancellationToken).ConfigureAwait(false);
                var movedMinimum = await GetSubtreeMinimumAsync(moved, cancellationToken).ConfigureAwait(false);
                await RefreshAncestorMinimumAsync(node.PageId, movedMinimum, cancellationToken).ConfigureAwait(false);
                return;
            }
        }
        if (nodeIndex + 1 < grandparent.Children.Count)
        {
            var right = await ReadInternalAsync(grandparent.Children[nodeIndex + 1], cancellationToken).ConfigureAwait(false);
            if (right.Children.Count > 2)
            {
                var moved = right.Children[0];
                var nodeChildren = node.Children.Append(moved).ToArray();
                var movedMinimum = await GetSubtreeMinimumAsync(moved, cancellationToken).ConfigureAwait(false);
                var nodeSeparators = node.Separators.Append(movedMinimum).ToArray();
                var rightChildren = right.Children.Skip(1).ToArray();
                var rightSeparators = right.Separators.Skip(1).ToArray();
                await WriteInternalAsync(node with { Children = nodeChildren, Separators = nodeSeparators }, cancellationToken)
                    .ConfigureAwait(false);
                await WriteInternalAsync(right with { Children = rightChildren, Separators = rightSeparators }, cancellationToken)
                    .ConfigureAwait(false);
                await SetParentAsync(moved, node.PageId, cancellationToken).ConfigureAwait(false);
                var rightMinimum = await GetSubtreeMinimumAsync(rightChildren[0], cancellationToken).ConfigureAwait(false);
                await RefreshAncestorMinimumAsync(right.PageId, rightMinimum, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        if (nodeIndex > 0)
        {
            var left = await ReadInternalAsync(grandparent.Children[nodeIndex - 1], cancellationToken).ConfigureAwait(false);
            var nodeMinimum = await GetSubtreeMinimumAsync(node.Children[0], cancellationToken).ConfigureAwait(false);
            var mergedChildren = left.Children.Concat(node.Children).ToArray();
            var mergedSeparators = left.Separators.Concat(new[] { nodeMinimum }).Concat(node.Separators).ToArray();
            if (!InternalIndexPageCodec.CanFit(_bufferPool.PageSize, mergedSeparators))
                throw new StorageResourceExhaustedException("Underfilled internal siblings cannot be merged.");
            await WriteInternalAsync(left with { Children = mergedChildren, Separators = mergedSeparators }, cancellationToken)
                .ConfigureAwait(false);
            foreach (var child in node.Children)
                await SetParentAsync(child, left.PageId, cancellationToken).ConfigureAwait(false);
            retiredPageIds.Add(node.PageId);
            await RemoveChildFromInternalAsync(grandparent, nodeIndex, retiredPageIds, cancellationToken).ConfigureAwait(false);
            return;
        }

        var rightNode = await ReadInternalAsync(grandparent.Children[1], cancellationToken).ConfigureAwait(false);
        var rightMinimumForMerge = await GetSubtreeMinimumAsync(rightNode.Children[0], cancellationToken).ConfigureAwait(false);
        var combinedChildren = node.Children.Concat(rightNode.Children).ToArray();
        var combinedSeparators = node.Separators.Concat(new[] { rightMinimumForMerge }).Concat(rightNode.Separators).ToArray();
        if (!InternalIndexPageCodec.CanFit(_bufferPool.PageSize, combinedSeparators))
            throw new StorageResourceExhaustedException("Underfilled internal siblings cannot be merged.");
        await WriteInternalAsync(node with { Children = combinedChildren, Separators = combinedSeparators }, cancellationToken)
            .ConfigureAwait(false);
        foreach (var child in rightNode.Children)
            await SetParentAsync(child, node.PageId, cancellationToken).ConfigureAwait(false);
        retiredPageIds.Add(rightNode.PageId);
        await RemoveChildFromInternalAsync(grandparent, 1, retiredPageIds, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<InternalIndexPage> ReadInternalAsync(PageId pageId, CancellationToken cancellationToken)
    {
        using var pin = await GetPinAsync(pageId, cancellationToken).ConfigureAwait(false);
        return InternalIndexPageCodec.Read(pin.Memory.Span, pageId);
    }

    private async ValueTask<IndexKey> GetSubtreeMinimumAsync(PageId pageId, CancellationToken cancellationToken)
    {
        var current = pageId;
        for (var height = 0; height < MaximumTreeHeight; height++)
        {
            using var pin = await GetPinAsync(current, cancellationToken).ConfigureAwait(false);
            var type = PageHeaderCodec.Read(pin.Memory.Span).PageType;
            if (type == PageType.BPlusTreeLeaf)
            {
                var leaf = LeafIndexPageCodec.Read(pin.Memory.Span, current);
                if (leaf.Entries.Count == 0) throw new StorageCorruptionException("A non-root subtree contains an empty leaf.");
                return leaf.Entries[0].Key;
            }
            if (type != PageType.BPlusTreeInternal)
                throw new StorageFormatException($"Expected index page, found {type} at {current}.");
            current = InternalIndexPageCodec.Read(pin.Memory.Span, current).Children[0];
        }
        throw new StorageCorruptionException($"Index tree exceeds maximum height {MaximumTreeHeight}.");
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
