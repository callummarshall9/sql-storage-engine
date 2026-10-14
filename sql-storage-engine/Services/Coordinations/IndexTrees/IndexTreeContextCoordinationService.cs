// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Runtime.CompilerServices;
using sql_storage_engine.Indexes;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Services.Orchestrations.IndexTreeResources;
using sql_storage_engine.Services.Orchestrations.LeafIndexPages;
using sql_storage_engine.Services.Orchestrations.InternalIndexPages;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Coordinations.IndexTrees;
internal sealed partial class IndexTreeContextCoordinationService : IIndexTreeContextCoordinationService
{
    private const int MaximumTreeHeight = 64;
    private const int MaximumScanPages = 8192;
    private readonly IndexTreeResourceOrchestrationService resources;
    private readonly LeafIndexPageOrchestrationService leaves;
    private readonly InternalIndexPageOrchestrationService internals;
    private readonly bool _isUnique;
    private IndexTreeContextCoordinationService(IndexTreeResourceOrchestrationService resources, LeafIndexPageOrchestrationService leaves, InternalIndexPageOrchestrationService internals, IndexTreeResourceConfiguration indexTreeResourceConfiguration)
    {
        this.resources = resources;
        this.leaves = leaves;
        this.internals = internals;
        _isUnique = indexTreeResourceConfiguration.IsUnique;
    }

    internal static IndexTreeContextCoordinationService CreateIndexTreeContextCoordinationService(IndexTreeResourceConfiguration indexTreeResourceConfiguration) =>
        new IndexTreeContextCoordinationService(resources: new IndexTreeResourceOrchestrationService(indexTreeResourceConfiguration: indexTreeResourceConfiguration), leaves: new LeafIndexPageOrchestrationService(), internals: new InternalIndexPageOrchestrationService(), indexTreeResourceConfiguration: indexTreeResourceConfiguration);

    public ValueTask<IndexTreeContext> CreateIndexTreeContextAsync(IndexTreeContext indexTreeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);

        var root = await AllocateAsync(pageType: PageType.BPlusTreeLeaf, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await WriteLeafAsync(leaf: new LeafIndexPage { PageId = root, Entries = [] }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await resources.UpdateIndexRootResourceAsync(rootPageId: root.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        indexTreeContext.RootPageId = root;
        return indexTreeContext;
    });

    public ValueTask<IndexTreeContext> AddIndexTreeContextAsync(IndexTreeContext newIndexTreeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIndexTreeContextOnAdd(indexTreeContext: newIndexTreeContext, cancellationToken: cancellationToken);

        await InsertAsync(key: newIndexTreeContext.Key!, rowId: newIndexTreeContext.RowId, includedPayload: newIndexTreeContext.IncludedPayload, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        newIndexTreeContext.RootPageId = new PageId
        {
            Value = resources.ReadIndexRootResource()
        };

        return newIndexTreeContext;
    });

    public ValueTask<IndexTreeContext> AddIndexTreeContextWithoutSplitAsync(IndexTreeContext newIndexTreeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIndexTreeContextWithoutSplitOnAdd(indexTreeContext: newIndexTreeContext, cancellationToken: cancellationToken);

        newIndexTreeContext.InsertResult = await InsertWithoutSplitAsync(key: newIndexTreeContext.Key!, rowId: newIndexTreeContext.RowId, includedPayload: newIndexTreeContext.IncludedPayload, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        newIndexTreeContext.RootPageId = new PageId
        {
            Value = resources.ReadIndexRootResource()
        };

        return newIndexTreeContext;
    });

    public ValueTask<IndexTreeContext> DeleteIndexTreeContextAsync(IndexTreeContext deletedIndexTreeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIndexTreeContextOnDelete(indexTreeContext: deletedIndexTreeContext, cancellationToken: cancellationToken);

        deletedIndexTreeContext.DeleteResult = await DeleteAsync(key: deletedIndexTreeContext.Key!, rowId: deletedIndexTreeContext.RowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return deletedIndexTreeContext;
    });

    public ValueTask<IndexTreeContext> ReadIndexTreeContextEntriesAsync(IndexTreeContext indexTreeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateKey(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);

        indexTreeContext.Entries = await FindEntriesAsync(key: indexTreeContext.Key!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return indexTreeContext;
    });

    public ValueTask<IndexTreeContext> ReadIndexTreeContextLeafPagesAsync(IndexTreeContext indexTreeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);

        indexTreeContext.PageIds = await GetLeafPageIdsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return indexTreeContext;
    });

    public IndexTreeContext ReadIndexTreeContext(IndexTreeContext indexTreeContext) =>
        TryCatch(operation: () =>
    {
        ValidateContext(indexTreeContext: indexTreeContext, cancellationToken: default);
        indexTreeContext.RootPageId = ReadRootPageId();
        indexTreeContext.IsUnique = _isUnique;
        return indexTreeContext;
    });

    public IndexTreeContext ReadIndexTreeContextScan(IndexTreeContext indexTreeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateRange(indexTreeContext: indexTreeContext, cancellationToken: cancellationToken);
        indexTreeContext.Rows = ScanAsync(range: indexTreeContext.Range!.Value, cancellationToken: cancellationToken);
        return indexTreeContext;
    });

    private PageId ReadRootPageId() =>
        new PageId
    {
        Value = resources.ReadIndexRootResource()
    };

    private async ValueTask<PageId> AllocateAsync(PageType pageType, CancellationToken cancellationToken) =>
        new PageId
    {
        Value = await resources.AllocateIndexPageResourceAsync(pageType: (ushort)pageType, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
    };

    private ValueTask UpdateRootAsync(PageId rootPageId, CancellationToken cancellationToken) =>
        resources.UpdateIndexRootResourceAsync(rootPageId: rootPageId.Value, cancellationToken: cancellationToken);

    private static LeafIndexEntry CreateLeafIndexEntry(IndexKey key, RowId rowId, ReadOnlyMemory<byte> includedPayload)
    {
        if (includedPayload.Length > ushort.MaxValue)
        {
            throw new ArgumentException(message: "Leaf included payload exceeds 65,535 bytes.", paramName: nameof(includedPayload));
        }

        return new LeafIndexEntry
        {
            Key = key,
            RowId = rowId,
            PayloadContent = new string (includedPayload.ToArray()
            .Select(selector: value => (char)value)
            .ToArray())
        };
    }

    private async ValueTask<IndexDeleteResult> DeleteAsync(sql_storage_engine.Models.Indexes.IndexKey key, RowId rowId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: key);
        List<PageId> retiredPageIds = [];

        PageId? current = await FindLeafAsync(key: key, equalRoutesLeft: true, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        HashSet<PageId> visited = [];

        while (current is { } leafId && visited.Add(item: leafId))
        {
            var leaf = await ReadLeafAsync(pageId: leafId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var entries = leaf.Entries.ToList();
            var entryIndex = entries.FindIndex(match: entry => entry.Key.Equals(other: key) && entry.RowId == rowId);

            if (entryIndex >= 0)
            {
                var removedMinimum = entryIndex == 0;
                entries.RemoveAt(index: entryIndex);

                var updated = leaf with
                {
                    Entries = entries.AsReadOnly()
                };

                if (leaf.ParentPageId is null)
                {
                    await WriteLeafAsync(leaf: updated, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                    return new IndexDeleteResult
                    {
                        Removed = true,
                        RetiredPageIds = retiredPageIds.AsReadOnly()
                    };
                }

                if (entries.Count < 2)
                {
                    await RebalanceLeafAsync(leaf: updated, retiredPageIds: retiredPageIds, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                }
                else
                {
                    await WriteLeafAsync(leaf: updated, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                    if (removedMinimum)
                    {
                        await RefreshAncestorMinimumAsync(childId: updated.PageId, minimum: updated.Entries[0].Key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
                    }
                }

                return new IndexDeleteResult
                {
                    Removed = true,
                    RetiredPageIds = retiredPageIds.AsReadOnly()
                };
            }

            if (leaf.Entries.Count > 0 && string.CompareOrdinal(strA: (leaf.Entries[^1].Key).Content, strB: (key).Content) > 0)
            {
                return new IndexDeleteResult
                {
                    Removed = false,
                    RetiredPageIds = retiredPageIds.AsReadOnly()
                };
            }

            current = leaf.NextPageId;
        }

        if (current is not null)
        {
            throw new StorageCorruptionException("Cycle detected while locating duplicate index entries.");
        }

        return new IndexDeleteResult
        {
            Removed = false,
            RetiredPageIds = retiredPageIds.AsReadOnly()
        };
    }

    private async ValueTask<IndexInsertResult> InsertWithoutSplitAsync(sql_storage_engine.Models.Indexes.IndexKey key, RowId rowId, ReadOnlyMemory<byte> includedPayload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: key);

        if (rowId.PageId.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowId));
        }

        if (!leaves.CheckLeafIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), leafIndexPage: new LeafIndexPage { CapacityEntries = [CreateLeafIndexEntry(key: key, rowId: rowId, includedPayload: includedPayload)] }))
        {
            throw new StorageResourceExhaustedException("Index key and covered payload do not fit on an empty leaf page.");
        }

        if (_isUnique && (await FindAsync(key: key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Count != 0)
        {
            throw new DuplicateIndexKeyException();
        }

        var leafId = await FindLeafAsync(key: key, equalRoutesLeft: false, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        sql_storage_engine.Models.Indexes.IndexKey? oldMinimum;

        {
            var pin = await GetPinAsync(pageId: leafId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                var leaf = leaves.ReadLeafIndexPage(page: pin.Memory.Span, expectedPageId: (leafId).Value);
                oldMinimum = leaf.Entries.Count == 0 ? null : leaf.Entries[0].Key;
                var entries = leaf.Entries.ToList();
                var insertAt = 0;

                while (insertAt < entries.Count && string.CompareOrdinal(strA: (entries[insertAt].Key).Content, strB: (key).Content) <= 0)
                {
                    insertAt++;
                }

                entries.Insert(index: insertAt, item: CreateLeafIndexEntry(key: key, rowId: rowId, includedPayload: includedPayload));

                if (!leaves.CheckLeafIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), leafIndexPage: new LeafIndexPage { CapacityEntries = entries }))
                {
                    return IndexInsertResult.SplitRequired;
                }

                leaves.WriteLeafIndexPage(page: pin.Memory.Span, leafIndexPage: leaf with { Entries = entries.AsReadOnly() });
                resources.MarkIndexPageLeaseDirty(indexPageLease: pin, logSequenceNumber: 0);
            }
            finally
            {
                resources.ReleaseIndexPageLease(indexPageLease: pin);
            }
        }

        if (oldMinimum is null || string.CompareOrdinal(strA: (key).Content, strB: (oldMinimum).Content) < 0)
        {
            await RefreshAncestorMinimumAsync(childId: leafId, minimum: key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return IndexInsertResult.Inserted;
    }

    private async ValueTask InsertAsync(sql_storage_engine.Models.Indexes.IndexKey key, RowId rowId, ReadOnlyMemory<byte> includedPayload, CancellationToken cancellationToken = default)
    {
        if (await InsertWithoutSplitAsync(key: key, rowId: rowId, includedPayload: includedPayload, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) == IndexInsertResult.Inserted)
        {
            return;
        }

        var leafId = await FindLeafAsync(key: key, equalRoutesLeft: false, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var leaf = await ReadLeafAsync(pageId: leafId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var entries = leaf.Entries.ToList();
        var insertAt = 0;

        while (insertAt < entries.Count && string.CompareOrdinal(strA: (entries[insertAt].Key).Content, strB: (key).Content) <= 0)
        {
            insertAt++;
        }

        entries.Insert(index: insertAt, item: CreateLeafIndexEntry(key: key, rowId: rowId, includedPayload: includedPayload));
        var splitAt = entries.Count / 2;
        var leftEntries = entries[..splitAt];
        var rightEntries = entries[splitAt..];

        if (!leaves.CheckLeafIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), leafIndexPage: new LeafIndexPage { CapacityEntries = leftEntries }) || !leaves.CheckLeafIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), leafIndexPage: new LeafIndexPage { CapacityEntries = rightEntries }))
        {
            throw new StorageResourceExhaustedException("Index key is too large to split into valid leaf pages.");
        }

        var rightId = await AllocateAsync(pageType: PageType.BPlusTreeLeaf, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await WriteLeafAsync(leaf: new LeafIndexPage { PageId = rightId, ParentPageId = leaf.ParentPageId, PreviousPageId = leafId, NextPageId = leaf.NextPageId, Entries = rightEntries.AsReadOnly() }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await WriteLeafAsync(leaf: leaf with { NextPageId = rightId, Entries = leftEntries.AsReadOnly() }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (leaf.NextPageId is { } oldNext)
        {
            var neighbor = await ReadLeafAsync(pageId: oldNext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (neighbor.PreviousPageId != leafId)
            {
                throw new StorageCorruptionException("Forward and reverse leaf links disagree before split.");
            }

            await WriteLeafAsync(leaf: neighbor with { PreviousPageId = rightId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        await InsertChildIntoParentAsync(leftId: leafId, rightId: rightId, leftMinimum: leftEntries[0].Key, rightMinimum: rightEntries[0].Key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask<IReadOnlyList<RowId>> FindAsync(sql_storage_engine.Models.Indexes.IndexKey key, CancellationToken cancellationToken = default) =>
        (await FindEntriesAsync(key: key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Select(selector: entry => entry.RowId)
            .ToArray();

    private async ValueTask<IReadOnlyList<sql_storage_engine.Models.Indexes.LeafIndexEntry>> FindEntriesAsync(sql_storage_engine.Models.Indexes.IndexKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(argument: key);

        PageId? leafId = await FindLeafAsync(key: key, equalRoutesLeft: true, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        List<sql_storage_engine.Models.Indexes.LeafIndexEntry> result = [];
        HashSet<PageId> visited = [];

        for (var pages = 0; pages < MaximumScanPages && leafId is { } current; pages++)
        {
            if (!visited.Add(item: current))
            {
                throw new StorageCorruptionException($"Cycle detected in leaf chain at {current}.");
            }

            var leaf = await ReadLeafAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var sawGreater = false;

            foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in leaf.Entries)
            {
                var comparison = string.CompareOrdinal(strA: (entry.Key).Content, strB: (key).Content);

                if (comparison == 0)
                {
                    result.Add(item: entry);
                }

                if (comparison > 0)
                {
                    sawGreater = true;
                    break;
                }
            }

            if (sawGreater || leaf.Entries.Count > 0 && string.CompareOrdinal(strA: (leaf.Entries[^1].Key).Content, strB: (key).Content) > 0)
            {
                return result.AsReadOnly();
            }

            leafId = leaf.NextPageId;
        }

        if (leafId is not null)
        {
            throw new StorageCorruptionException("Exact lookup exceeded the leaf traversal bound.");
        }

        return result.AsReadOnly();
    }

    private async IAsyncEnumerable<sql_storage_engine.Models.Indexes.LeafIndexEntry> ScanAsync(IndexRange range, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (range.LowerBound is not null && range.UpperBound is not null && string.CompareOrdinal(strA: (range.LowerBound).Content, strB: (range.UpperBound).Content) > 0)
        {
            yield break;
        }

        var current = range.Direction == ScanDirection.Ascending ? await FindEdgeLeafAsync(leftmost: true, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false) : await FindEdgeLeafAsync(leftmost: false, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        HashSet<PageId> visited = [];

        for (var pages = 0; pages < MaximumScanPages && current is { } pageId; pages++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!visited.Add(item: pageId))
            {
                throw new StorageCorruptionException($"Cycle detected in leaf scan at {pageId}.");
            }

            var leaf = await ReadLeafAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var entries = range.Direction == ScanDirection.Ascending ? leaf.Entries : leaf.Entries.Reverse()
            .ToArray();

            foreach (sql_storage_engine.Models.Indexes.LeafIndexEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var aboveLower = range.LowerBound is null || string.CompareOrdinal(strA: (entry.Key).Content, strB: (range.LowerBound).Content)is var lower && (lower > 0 || lower == 0 && range.IncludeLowerBound);
                var belowUpper = range.UpperBound is null || string.CompareOrdinal(strA: (entry.Key).Content, strB: (range.UpperBound).Content)is var upper && (upper < 0 || upper == 0 && range.IncludeUpperBound);

                if (aboveLower && belowUpper)
                {
                    yield return entry;
                }
            }

            current = range.Direction == ScanDirection.Ascending ? leaf.NextPageId : leaf.PreviousPageId;
        }

        if (current is not null)
        {
            throw new StorageCorruptionException("Range scan exceeded the leaf traversal bound.");
        }
    }

    private async ValueTask<IReadOnlyList<PageId>> GetLeafPageIdsAsync(CancellationToken cancellationToken = default)
    {
        var current = await FindEdgeLeafAsync(leftmost: true, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        List<PageId> pages = [];
        HashSet<PageId> visited = [];

        while (current is { } pageId)
        {
            if (pages.Count == MaximumScanPages)
            {
                throw new StorageResourceExhaustedException("Index leaf traversal limit exceeded.");
            }

            if (!visited.Add(item: pageId))
            {
                throw new StorageCorruptionException($"Cycle detected in leaf chain at {pageId}.");
            }

            pages.Add(item: pageId);

            current = (await ReadLeafAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).NextPageId;
        }

        return pages.AsReadOnly();
    }

    private async ValueTask<PageId> FindLeafAsync(sql_storage_engine.Models.Indexes.IndexKey key, bool equalRoutesLeft, CancellationToken cancellationToken)
    {
        var current = ReadRootPageId();
        HashSet<PageId> visited = [];

        for (var height = 0; height < MaximumTreeHeight; height++)
        {
            if (!visited.Add(item: current))
            {
                throw new StorageCorruptionException($"Cycle detected in index tree at {current}.");
            }

            var pin = await GetPinAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                var type = leaves.ReadLeafIndexPageType(page: pin.Memory.Span);

                if (type == PageType.BPlusTreeLeaf)
                {
                    _ = leaves.ReadLeafIndexPage(page: pin.Memory.Span, expectedPageId: (current).Value);
                    return current;
                }

                if (type != PageType.BPlusTreeInternal)
                {
                    throw new StorageFormatException($"Expected index page, found {type} at {current}.");
                }

                var node = internals.ReadInternalIndexPage(page: pin.Memory.Span, expectedPageId: (current).Value);
                var childIndex = 0;

                while (childIndex < node.Separators.Count && (equalRoutesLeft ? string.CompareOrdinal(strA: (node.Separators[childIndex]).Content, strB: (key).Content) < 0 : string.CompareOrdinal(strA: (node.Separators[childIndex]).Content, strB: (key).Content) <= 0))
                {
                    childIndex++;
                }

                current = node.Children[childIndex];
            }
            finally
            {
                resources.ReleaseIndexPageLease(indexPageLease: pin);
            }
        }

        throw new StorageCorruptionException($"Index tree exceeds maximum height {MaximumTreeHeight}.");
    }

    private async ValueTask<PageId?> FindEdgeLeafAsync(bool leftmost, CancellationToken cancellationToken)
    {
        var current = ReadRootPageId();
        HashSet<PageId> visited = [];

        for (var height = 0; height < MaximumTreeHeight; height++)
        {
            if (!visited.Add(item: current))
            {
                throw new StorageCorruptionException($"Cycle detected in index tree at {current}.");
            }

            var pin = await GetPinAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                var type = leaves.ReadLeafIndexPageType(page: pin.Memory.Span);

                if (type == PageType.BPlusTreeLeaf)
                {
                    _ = leaves.ReadLeafIndexPage(page: pin.Memory.Span, expectedPageId: (current).Value);
                    return current;
                }

                if (type != PageType.BPlusTreeInternal)
                {
                    throw new StorageFormatException($"Expected index page, found {type} at {current}.");
                }

                var node = internals.ReadInternalIndexPage(page: pin.Memory.Span, expectedPageId: (current).Value);
                current = leftmost ? node.Children[0] : node.Children[^1];
            }
            finally
            {
                resources.ReleaseIndexPageLease(indexPageLease: pin);
            }
        }

        throw new StorageCorruptionException($"Index tree exceeds maximum height {MaximumTreeHeight}.");
    }

    private async ValueTask<LeafIndexPage> ReadLeafAsync(PageId pageId, CancellationToken cancellationToken)
    {
        var pin = await GetPinAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            return leaves.ReadLeafIndexPage(page: pin.Memory.Span, expectedPageId: (pageId).Value);
        }
        finally
        {
            resources.ReleaseIndexPageLease(indexPageLease: pin);
        }
    }

    private async ValueTask<IndexPageLease> GetPinAsync(PageId pageId, CancellationToken cancellationToken)
    {
        try
        {
            return await resources.AcquireIndexPageLeaseAsync(pageId: pageId.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (StorageResourceException exception)
        {
            throw new StorageCorruptionException($"Index references inaccessible {pageId}.", exception);
        }
    }

    private async ValueTask WriteLeafAsync(LeafIndexPage leaf, CancellationToken cancellationToken)
    {
        var pin = await GetPinAsync(pageId: leaf.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            leaves.WriteLeafIndexPage(page: pin.Memory.Span, leafIndexPage: leaf);
            resources.MarkIndexPageLeaseDirty(indexPageLease: pin, logSequenceNumber: 0);
        }
        finally
        {
            resources.ReleaseIndexPageLease(indexPageLease: pin);
        }
    }

    private async ValueTask WriteInternalAsync(InternalIndexPage node, CancellationToken cancellationToken)
    {
        var pin = await GetPinAsync(pageId: node.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            internals.WriteInternalIndexPage(page: pin.Memory.Span, internalIndexPage: node);
            resources.MarkIndexPageLeaseDirty(indexPageLease: pin, logSequenceNumber: 0);
        }
        finally
        {
            resources.ReleaseIndexPageLease(indexPageLease: pin);
        }
    }

    private async ValueTask InsertChildIntoParentAsync(PageId leftId, PageId rightId, sql_storage_engine.Models.Indexes.IndexKey leftMinimum, sql_storage_engine.Models.Indexes.IndexKey rightMinimum, CancellationToken cancellationToken)
    {
        var parentId = await ReadParentIdAsync(pageId: leftId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (parentId is null)
        {
            var rootId = await AllocateAsync(pageType: PageType.BPlusTreeInternal, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await WriteInternalAsync(node: new InternalIndexPage { PageId = rootId, ParentPageId = null, Separators = new[] { rightMinimum }, Children = new[] { leftId, rightId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await SetParentAsync(pageId: leftId, parentId: rootId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await SetParentAsync(pageId: rightId, parentId: rootId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await UpdateRootAsync(rootPageId: rootId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return;
        }

        InternalIndexPage parent;

        {
            var pin = await GetPinAsync(pageId: parentId.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                parent = internals.ReadInternalIndexPage(page: pin.Memory.Span, expectedPageId: (parentId.Value).Value);
            }
            finally
            {
                resources.ReleaseIndexPageLease(indexPageLease: pin);
            }
        }

        var children = parent.Children.ToList();
        var childIndex = children.IndexOf(item: leftId);

        if (childIndex < 0)
        {
            throw new StorageCorruptionException($"Parent {parent.PageId} does not reference child {leftId}.");
        }

        var separators = parent.Separators.ToList();
        separators.Insert(index: childIndex, item: rightMinimum);
        children.Insert(index: childIndex + 1, item: rightId);

        if (childIndex > 0)
        {
            separators[childIndex - 1] = leftMinimum;
        }

        if (internals.CheckInternalIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), internalIndexPage: new InternalIndexPage { CapacitySeparators = separators }))
        {
            await WriteInternalAsync(node: parent with { Separators = separators.AsReadOnly(), Children = children.AsReadOnly() }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await SetParentAsync(pageId: rightId, parentId: parent.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (childIndex == 0)
            {
                await RefreshAncestorMinimumAsync(childId: parent.PageId, minimum: leftMinimum, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            return;
        }

        var splitChildIndex = ChooseInternalSplit(children: children, separators: separators);
        sql_storage_engine.Models.Indexes.IndexKey promoted = separators[splitChildIndex - 1];
        var leftChildren = children[..splitChildIndex];
        var rightChildren = children[splitChildIndex..];
        var leftSeparators = separators[..(splitChildIndex - 1)];
        var rightSeparators = separators[splitChildIndex..];

        var newRightId = await AllocateAsync(pageType: PageType.BPlusTreeInternal, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await WriteInternalAsync(node: parent with { Separators = leftSeparators.AsReadOnly(), Children = leftChildren.AsReadOnly() }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await WriteInternalAsync(node: new InternalIndexPage { PageId = newRightId, ParentPageId = parent.ParentPageId, Separators = rightSeparators.AsReadOnly(), Children = rightChildren.AsReadOnly() }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        foreach (var movedChild in rightChildren)
        {
            await SetParentAsync(pageId: movedChild, parentId: newRightId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        await InsertChildIntoParentAsync(leftId: parent.PageId, rightId: newRightId, leftMinimum: leftMinimum, rightMinimum: promoted, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private int ChooseInternalSplit(IReadOnlyList<PageId> children, IReadOnlyList<sql_storage_engine.Models.Indexes.IndexKey> separators)
    {
        var preferred = children.Count / 2;

        return Enumerable.Range(start: 2, count: children.Count - 3)
            .OrderBy(keySelector: index => Math.Abs(value: index - preferred))
            .FirstOrDefault(predicate: index => internals.CheckInternalIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), internalIndexPage: new InternalIndexPage { CapacitySeparators = separators.Take(count: index - 1) }) && internals.CheckInternalIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), internalIndexPage: new InternalIndexPage { CapacitySeparators = separators.Skip(count: index) })) switch
        {
            0 => throw new StorageResourceExhaustedException("Internal entries cannot be redistributed into two valid pages."),
            var index => index
        };
    }

    private async ValueTask<PageId?> ReadParentIdAsync(PageId pageId, CancellationToken cancellationToken)
    {
        var pin = await GetPinAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            return leaves.ReadLeafIndexPageType(page: pin.Memory.Span) switch
            {
                PageType.BPlusTreeLeaf => leaves.ReadLeafIndexPage(page: pin.Memory.Span, expectedPageId: (pageId).Value)
            .ParentPageId,
                PageType.BPlusTreeInternal => internals.ReadInternalIndexPage(page: pin.Memory.Span, expectedPageId: (pageId).Value)
            .ParentPageId,
                var type => throw new StorageFormatException($"Expected index child, found {type}.")};
        }
        finally
        {
            resources.ReleaseIndexPageLease(indexPageLease: pin);
        }
    }

    private async ValueTask SetParentAsync(PageId pageId, PageId? parentId, CancellationToken cancellationToken)
    {
        var pin = await GetPinAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            switch (leaves.ReadLeafIndexPageType(page: pin.Memory.Span))
            {
                case PageType.BPlusTreeLeaf:
                    var leaf = leaves.ReadLeafIndexPage(page: pin.Memory.Span, expectedPageId: (pageId).Value);
                    leaves.WriteLeafIndexPage(page: pin.Memory.Span, leafIndexPage: leaf with { ParentPageId = parentId });
                    break;
                case PageType.BPlusTreeInternal:
                    var node = internals.ReadInternalIndexPage(page: pin.Memory.Span, expectedPageId: (pageId).Value);
                    internals.WriteInternalIndexPage(page: pin.Memory.Span, internalIndexPage: node with { ParentPageId = parentId });
                    break;
                default:
                    throw new StorageFormatException("Cannot assign a parent to a non-index page.");
            }

            resources.MarkIndexPageLeaseDirty(indexPageLease: pin, logSequenceNumber: 0);
        }
        finally
        {
            resources.ReleaseIndexPageLease(indexPageLease: pin);
        }
    }

    private async ValueTask RebalanceLeafAsync(LeafIndexPage leaf, List<PageId> retiredPageIds, CancellationToken cancellationToken)
    {
        if (leaf.ParentPageId is not { } parentId)
        {
            throw new StorageCorruptionException("A non-root leaf has no parent.");
        }

        var parent = await ReadInternalAsync(pageId: parentId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var childIndex = parent.Children.ToList()
            .IndexOf(item: leaf.PageId);

        if (childIndex < 0)
        {
            throw new StorageCorruptionException($"Parent {parentId} does not reference leaf {leaf.PageId}.");
        }

        if (childIndex > 0)
        {
            var left = await ReadLeafAsync(pageId: parent.Children[childIndex - 1], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (left.Entries.Count > 2)
            {
                var leftEntries = left.Entries.ToList();
                var leafEntries = leaf.Entries.ToList();
                leafEntries.Insert(index: 0, item: leftEntries[^1]);
                leftEntries.RemoveAt(index: leftEntries.Count - 1);

                var updatedLeft = left with
                {
                    Entries = leftEntries.AsReadOnly()
                };

                var updatedLeaf = leaf with
                {
                    Entries = leafEntries.AsReadOnly()
                };

                await WriteLeafAsync(leaf: updatedLeft, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await WriteLeafAsync(leaf: updatedLeaf, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await RefreshAncestorMinimumAsync(childId: updatedLeaf.PageId, minimum: updatedLeaf.Entries[0].Key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                return;
            }
        }

        if (childIndex + 1 < parent.Children.Count)
        {
            var right = await ReadLeafAsync(pageId: parent.Children[childIndex + 1], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (right.Entries.Count > 2)
            {
                var rightEntries = right.Entries.ToList();
                var leafEntries = leaf.Entries.ToList();
                leafEntries.Add(item: rightEntries[0]);
                rightEntries.RemoveAt(index: 0);

                var updatedLeaf = leaf with
                {
                    Entries = leafEntries.AsReadOnly()
                };

                var updatedRight = right with
                {
                    Entries = rightEntries.AsReadOnly()
                };

                await WriteLeafAsync(leaf: updatedLeaf, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await WriteLeafAsync(leaf: updatedRight, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await RefreshAncestorMinimumAsync(childId: updatedLeaf.PageId, minimum: updatedLeaf.Entries[0].Key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await RefreshAncestorMinimumAsync(childId: updatedRight.PageId, minimum: updatedRight.Entries[0].Key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                return;
            }
        }

        if (childIndex > 0)
        {
            var left = await ReadLeafAsync(pageId: parent.Children[childIndex - 1], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var mergedEntries = left.Entries.Concat(second: leaf.Entries)
            .ToArray();

            if (!leaves.CheckLeafIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), leafIndexPage: new LeafIndexPage { CapacityEntries = mergedEntries }))
            {
                throw new StorageResourceExhaustedException("Underfilled leaf siblings cannot be merged.");
            }

            await WriteLeafAsync(leaf: left with { NextPageId = leaf.NextPageId, Entries = mergedEntries }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (leaf.NextPageId is { } nextId)
            {
                var next = await ReadLeafAsync(pageId: nextId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await WriteLeafAsync(leaf: next with { PreviousPageId = left.PageId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            retiredPageIds.Add(item: leaf.PageId);

            await RemoveChildFromInternalAsync(node: parent, childIndex: childIndex, retiredPageIds: retiredPageIds, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return;
        }

        var rightSibling = await ReadLeafAsync(pageId: parent.Children[1], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var rightMergedEntries = leaf.Entries.Concat(second: rightSibling.Entries)
            .ToArray();

        if (!leaves.CheckLeafIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), leafIndexPage: new LeafIndexPage { CapacityEntries = rightMergedEntries }))
        {
            throw new StorageResourceExhaustedException("Underfilled leaf siblings cannot be merged.");
        }

        await WriteLeafAsync(leaf: leaf with { NextPageId = rightSibling.NextPageId, Entries = rightMergedEntries }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (rightSibling.NextPageId is { } followingId)
        {
            var following = await ReadLeafAsync(pageId: followingId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await WriteLeafAsync(leaf: following with { PreviousPageId = leaf.PageId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        retiredPageIds.Add(item: rightSibling.PageId);

        await RemoveChildFromInternalAsync(node: parent, childIndex: 1, retiredPageIds: retiredPageIds, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        await RefreshAncestorMinimumAsync(childId: leaf.PageId, minimum: rightMergedEntries[0].Key, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask RemoveChildFromInternalAsync(InternalIndexPage node, int childIndex, List<PageId> retiredPageIds, CancellationToken cancellationToken)
    {
        var children = node.Children.ToList();
        var separators = node.Separators.ToList();
        children.RemoveAt(index: childIndex);
        separators.RemoveAt(index: childIndex == 0 ? 0 : childIndex - 1);

        if (children.Count >= 2)
        {
            await WriteInternalAsync(node: node with { Children = children.AsReadOnly(), Separators = separators.AsReadOnly() }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return;
        }

        if (node.ParentPageId is null)
        {
            await SetParentAsync(pageId: children[0], parentId: null, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            await UpdateRootAsync(rootPageId: children[0], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            retiredPageIds.Add(item: node.PageId);
            return;
        }

        await RebalanceInternalAsync(node: node with { Children = children.AsReadOnly(), Separators = separators.AsReadOnly() }, retiredPageIds: retiredPageIds, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask RebalanceInternalAsync(InternalIndexPage node, List<PageId> retiredPageIds, CancellationToken cancellationToken)
    {
        var grandparent = await ReadInternalAsync(pageId: node.ParentPageId!.Value, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var nodeIndex = grandparent.Children.ToList()
            .IndexOf(item: node.PageId);

        if (nodeIndex < 0)
        {
            throw new StorageCorruptionException($"Parent {grandparent.PageId} does not reference {node.PageId}.");
        }

        if (nodeIndex > 0)
        {
            var left = await ReadInternalAsync(pageId: grandparent.Children[nodeIndex - 1], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (left.Children.Count > 2)
            {
                var moved = left.Children[^1];

                var leftChildren = left.Children.Take(count: left.Children.Count - 1)
            .ToArray();

                var leftSeparators = left.Separators.Take(count: left.Separators.Count - 1)
            .ToArray();

                var nodeChildren = node.Children.Prepend(element: moved)
            .ToArray();

                sql_storage_engine.Models.Indexes.IndexKey oldMinimum = await GetSubtreeMinimumAsync(pageId: node.Children[0], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                var nodeSeparators = node.Separators.Prepend(element: oldMinimum)
            .ToArray();

                await WriteInternalAsync(node: left with { Children = leftChildren, Separators = leftSeparators }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await WriteInternalAsync(node: node with { Children = nodeChildren, Separators = nodeSeparators }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await SetParentAsync(pageId: moved, parentId: node.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                sql_storage_engine.Models.Indexes.IndexKey movedMinimum = await GetSubtreeMinimumAsync(pageId: moved, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await RefreshAncestorMinimumAsync(childId: node.PageId, minimum: movedMinimum, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                return;
            }
        }

        if (nodeIndex + 1 < grandparent.Children.Count)
        {
            var right = await ReadInternalAsync(pageId: grandparent.Children[nodeIndex + 1], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            if (right.Children.Count > 2)
            {
                var moved = right.Children[0];

                var nodeChildren = node.Children.Append(element: moved)
            .ToArray();

                sql_storage_engine.Models.Indexes.IndexKey movedMinimum = await GetSubtreeMinimumAsync(pageId: moved, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                var nodeSeparators = node.Separators.Append(element: movedMinimum)
            .ToArray();

                var rightChildren = right.Children.Skip(count: 1)
            .ToArray();

                var rightSeparators = right.Separators.Skip(count: 1)
            .ToArray();

                await WriteInternalAsync(node: node with { Children = nodeChildren, Separators = nodeSeparators }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await WriteInternalAsync(node: right with { Children = rightChildren, Separators = rightSeparators }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await SetParentAsync(pageId: moved, parentId: node.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                sql_storage_engine.Models.Indexes.IndexKey rightMinimum = await GetSubtreeMinimumAsync(pageId: rightChildren[0], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                await RefreshAncestorMinimumAsync(childId: right.PageId, minimum: rightMinimum, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                return;
            }
        }

        if (nodeIndex > 0)
        {
            var left = await ReadInternalAsync(pageId: grandparent.Children[nodeIndex - 1], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            sql_storage_engine.Models.Indexes.IndexKey nodeMinimum = await GetSubtreeMinimumAsync(pageId: node.Children[0], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            var mergedChildren = left.Children.Concat(second: node.Children)
            .ToArray();

            var mergedSeparators = left.Separators.Concat(second: new[] { nodeMinimum })
            .Concat(second: node.Separators)
            .ToArray();

            if (!internals.CheckInternalIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), internalIndexPage: new InternalIndexPage { CapacitySeparators = mergedSeparators }))
            {
                throw new StorageResourceExhaustedException("Underfilled internal siblings cannot be merged.");
            }

            await WriteInternalAsync(node: left with { Children = mergedChildren, Separators = mergedSeparators }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            foreach (var child in node.Children)
            {
                await SetParentAsync(pageId: child, parentId: left.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
            }

            retiredPageIds.Add(item: node.PageId);

            await RemoveChildFromInternalAsync(node: grandparent, childIndex: nodeIndex, retiredPageIds: retiredPageIds, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            return;
        }

        var rightNode = await ReadInternalAsync(pageId: grandparent.Children[1], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        sql_storage_engine.Models.Indexes.IndexKey rightMinimumForMerge = await GetSubtreeMinimumAsync(pageId: rightNode.Children[0], cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var combinedChildren = node.Children.Concat(second: rightNode.Children)
            .ToArray();

        var combinedSeparators = node.Separators.Concat(second: new[] { rightMinimumForMerge })
            .Concat(second: rightNode.Separators)
            .ToArray();

        if (!internals.CheckInternalIndexPageCapacity(pageSize: resources.ReadIndexPageResourceSize(), internalIndexPage: new InternalIndexPage { CapacitySeparators = combinedSeparators }))
        {
            throw new StorageResourceExhaustedException("Underfilled internal siblings cannot be merged.");
        }

        await WriteInternalAsync(node: node with { Children = combinedChildren, Separators = combinedSeparators }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        foreach (var child in rightNode.Children)
        {
            await SetParentAsync(pageId: child, parentId: node.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        retiredPageIds.Add(item: rightNode.PageId);

        await RemoveChildFromInternalAsync(node: grandparent, childIndex: 1, retiredPageIds: retiredPageIds, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    private async ValueTask<InternalIndexPage> ReadInternalAsync(PageId pageId, CancellationToken cancellationToken)
    {
        var pin = await GetPinAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            return internals.ReadInternalIndexPage(page: pin.Memory.Span, expectedPageId: (pageId).Value);
        }
        finally
        {
            resources.ReleaseIndexPageLease(indexPageLease: pin);
        }
    }

    private async ValueTask<sql_storage_engine.Models.Indexes.IndexKey> GetSubtreeMinimumAsync(PageId pageId, CancellationToken cancellationToken)
    {
        var current = pageId;

        for (var height = 0; height < MaximumTreeHeight; height++)
        {
            var pin = await GetPinAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                var type = leaves.ReadLeafIndexPageType(page: pin.Memory.Span);

                if (type == PageType.BPlusTreeLeaf)
                {
                    var leaf = leaves.ReadLeafIndexPage(page: pin.Memory.Span, expectedPageId: (current).Value);

                    if (leaf.Entries.Count == 0)
                    {
                        throw new StorageCorruptionException("A non-root subtree contains an empty leaf.");
                    }

                    return leaf.Entries[0].Key;
                }

                if (type != PageType.BPlusTreeInternal)
                {
                    throw new StorageFormatException($"Expected index page, found {type} at {current}.");
                }

                current = internals.ReadInternalIndexPage(page: pin.Memory.Span, expectedPageId: (current).Value)
            .Children[0];
            }
            finally
            {
                resources.ReleaseIndexPageLease(indexPageLease: pin);
            }
        }

        throw new StorageCorruptionException($"Index tree exceeds maximum height {MaximumTreeHeight}.");
    }

    private async ValueTask RefreshAncestorMinimumAsync(PageId childId, sql_storage_engine.Models.Indexes.IndexKey minimum, CancellationToken cancellationToken)
    {
        PageId? parentId;

        {
            var childPin = await GetPinAsync(pageId: childId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                var type = leaves.ReadLeafIndexPageType(page: childPin.Memory.Span);

                parentId = type switch
                {
                    PageType.BPlusTreeLeaf => leaves.ReadLeafIndexPage(page: childPin.Memory.Span, expectedPageId: (childId).Value)
            .ParentPageId,
                    PageType.BPlusTreeInternal => internals.ReadInternalIndexPage(page: childPin.Memory.Span, expectedPageId: (childId).Value)
            .ParentPageId,
                    _ => throw new StorageFormatException("Index ancestor has the wrong page type.")};
            }
            finally
            {
                resources.ReleaseIndexPageLease(indexPageLease: childPin);
            }
        }

        if (parentId is not { } parent)
        {
            return;
        }

        var propagate = false;

        {
            var parentPin = await GetPinAsync(pageId: parent, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                var node = internals.ReadInternalIndexPage(page: parentPin.Memory.Span, expectedPageId: (parent).Value);

                var childIndex = node.Children.ToList()
            .IndexOf(item: childId);

                if (childIndex < 0)
                {
                    throw new StorageCorruptionException($"Parent {parent} does not reference child {childId}.");
                }

                if (childIndex == 0)
                {
                    propagate = true;
                }
                else
                {
                    var separators = node.Separators.ToList();
                    separators[childIndex - 1] = minimum;
                    internals.WriteInternalIndexPage(page: parentPin.Memory.Span, internalIndexPage: node with { Separators = separators.AsReadOnly() });
                    resources.MarkIndexPageLeaseDirty(indexPageLease: parentPin, logSequenceNumber: 0);
                }
            }
            finally
            {
                resources.ReleaseIndexPageLease(indexPageLease: parentPin);
            }
        }

        if (propagate)
        {
            await RefreshAncestorMinimumAsync(childId: parent, minimum: minimum, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    public IndexTreeContext ReadIndexTreeContextBinding(IndexTreeContext indexTreeContext) =>
        TryCatch(operation: () =>
    {
        ValidateBinding(indexTreeContext: indexTreeContext);
        return indexTreeContext;
    });

    public ValueTask<IndexTreeContext> AddIndexTreeContextBindingAsync(IndexTreeContext newIndexTreeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIndexTreeContextBindingOnAdd(indexTreeContext: newIndexTreeContext, cancellationToken: cancellationToken);

        await InsertAsync(key: newIndexTreeContext.Key!, rowId: newIndexTreeContext.RowId, includedPayload: newIndexTreeContext.IncludedPayload, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        newIndexTreeContext.Binding!.AddCount++;
        return newIndexTreeContext;
    });

    public ValueTask<IndexTreeContext> DeleteIndexTreeContextBindingAsync(IndexTreeContext deletedIndexTreeContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIndexTreeContextBindingOnDelete(indexTreeContext: deletedIndexTreeContext, cancellationToken: cancellationToken);

        deletedIndexTreeContext.DeleteResult = await DeleteAsync(key: deletedIndexTreeContext.Key!, rowId: deletedIndexTreeContext.RowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (deletedIndexTreeContext.DeleteResult.Removed)
        {
            deletedIndexTreeContext.Binding!.RemoveCount++;
        }

        return deletedIndexTreeContext;
    });
}