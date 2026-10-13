// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Heap;
using System.Runtime.CompilerServices;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Orchestrations.HeapResources;
using sql_storage_engine.Services.Orchestrations.HeapPageLayouts;

namespace sql_storage_engine.Services.Coordinations.TableHeaps;
internal sealed partial class TableHeapContextCoordinationService : ITableHeapContextCoordinationService
{
    private readonly HeapPageResourceOrchestrationService resources;
    private readonly HeapPageLayoutOrchestrationService layouts;
    private readonly TableHeapContext state;
    private TableHeapContextCoordinationService(HeapPageResourceOrchestrationService resources, HeapPageLayoutOrchestrationService layouts, HeapPageResourceConfiguration heapPageResourceConfiguration)
    {
        this.resources = resources;
        this.layouts = layouts;
        state = new TableHeapContext
        {
            RootPageId = new PageId
            {
                Value = heapPageResourceConfiguration.RootPageId
            },
            KnownPages = new HashSet<PageId>
            {
                new PageId
                {
                    Value = heapPageResourceConfiguration.RootPageId
                }
            }
        };
    }

    internal static TableHeapContextCoordinationService CreateTableHeapContextCoordinationService(HeapPageResourceConfiguration heapPageResourceConfiguration) =>
        new TableHeapContextCoordinationService(resources: HeapPageResourceOrchestrationService.CreateHeapPageResourceOrchestrationService(heapPageResourceConfiguration: heapPageResourceConfiguration), layouts: new HeapPageLayoutOrchestrationService(), heapPageResourceConfiguration: heapPageResourceConfiguration);

    public ValueTask<TableHeapContext> CreateTableHeapContextAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);

        var root = await AllocatePageAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var pin = await AcquirePageAsync(pageId: root, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            layouts.InitializeHeapPageLayout(page: pin.Memory.Span, pageId: root.Value, previousPageId: null, nextPageId: null);
            var page = BindHeapPageLayout(page: pin.Memory, pageId: root);
            resources.MarkHeapPageLeaseDirty(heapPageLease: pin, logSequenceNumber: 0);
            state.RootPageId = root;
            state.KnownPages!.Clear();
            state.KnownPages.Add(item: root);
            UpdateFreeSpace(pageId: root, freeBytes: ReadFreeBytes(heapPageLayout: page));
            return state;
        }
        finally
        {
            resources.ReleaseHeapPageLease(heapPageLease: pin);
        }
    });

    public ValueTask<TableHeapContext> OpenTableHeapContextAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);

        await RebuildFreeSpaceMapCoreAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return state;
    });

    public TableHeapContext ReadTableHeapContext(TableHeapContext tableHeapContext) =>
        TryCatch(operation: () =>
    {
        ValidateContextSource(tableHeapContext: tableHeapContext);

        return state with
        {
        };
    });

    public ValueTask<TableHeapContext> AddTableHeapContextAsync(TableHeapContext newTableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTableHeapContextOnAdd(newTableHeapContext: newTableHeapContext, cancellationToken: cancellationToken);

        var rowId = await InsertCoreAsync(row: newTableHeapContext.Row, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return newTableHeapContext with
        {
            ResultRowId = rowId
        };
    });

    public ValueTask<TableHeapContext> DeleteTableHeapContextAsync(TableHeapContext deletedTableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTableHeapContextOnDelete(tableHeapContext: deletedTableHeapContext, cancellationToken: cancellationToken);

        var deleted = await DeleteCoreAsync(rowId: deletedTableHeapContext.RowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return deletedTableHeapContext with
        {
            Result = deleted
        };
    });

    public ValueTask<TableHeapContext> UpdateTableHeapContextAsync(TableHeapContext updatedTableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateTableHeapContextOnUpdate(tableHeapContext: updatedTableHeapContext, cancellationToken: cancellationToken);

        var result = await UpdateCoreAsync(rowId: updatedTableHeapContext.RowId, row: updatedTableHeapContext.Row, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return updatedTableHeapContext with
        {
            UpdateResult = result
        };
    });

    public ValueTask<TableHeapContext> CompactTableHeapContextAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);

        var result = await CompactPageWithResultCoreAsync(pageId: tableHeapContext.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return tableHeapContext with
        {
            Result = result.Compacted,
            ReclaimedBytes = result.ReclaimedBytes
        };
    });

    public ValueTask<TableHeapContext> ReadTableHeapContextPageIdsAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaximumPages(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);

        var ids = await GetPageIdsCoreAsync(maximumPages: tableHeapContext.MaximumPages, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return tableHeapContext with
        {
            PageIds = ids
        };
    });

    public ValueTask RebuildTableHeapContextFreeSpacesAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);

        await RebuildFreeSpaceMapCoreAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<TableHeapContext> ReadTableHeapContextRowAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);

        var result = await ReadCoreAsync(rowId: tableHeapContext.RowId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return tableHeapContext with
        {
            LookupResult = result.Result,
            Row = result.Row
        };
    });

    public TableHeapContext ReadTableHeapContextScan(TableHeapContext tableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateContext(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);

        return tableHeapContext with
        {
            Rows = GuardRows(source: ScanCoreAsync(cancellationToken: cancellationToken), cancellationToken: cancellationToken)
        };
    });

    public TableHeapContext ReadTableHeapContextSample(TableHeapContext tableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateSample(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);

        return tableHeapContext with
        {
            Rows = GuardRows(source: SampleCoreAsync(pagePercentage: tableHeapContext.PagePercentage, repeatableSeed: tableHeapContext.RepeatableSeed, cancellationToken: cancellationToken), cancellationToken: cancellationToken)
        };
    });

    public ValueTask<TableHeapContext> ReadTableHeapContextLiveCountAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateContext(tableHeapContext: tableHeapContext, cancellationToken: cancellationToken);

        var count = await GetLiveRowCountCoreAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return tableHeapContext with
        {
            LiveRowCount = count
        };
    });

    public TableHeapContext FindTableHeapContextFreeSpacePage(TableHeapContext tableHeapContext) =>
        TryCatch(operation: () =>
    {
        ValidateRequiredBytes(tableHeapContext: tableHeapContext);

        return tableHeapContext with
        {
            ResultPageId = FindFreeSpacePage(requiredBytes: tableHeapContext.RequiredBytes)
        };
    });

    public void SetTableHeapContextFreeSpace(TableHeapContext tableHeapContext, int freeBytes) =>
        TryCatch(operation: () =>
    {
        ValidateFreeSpace(tableHeapContext: tableHeapContext, freeBytes: freeBytes);
        UpdateFreeSpace(pageId: tableHeapContext.PageId, freeBytes: freeBytes);
    });

    public void RemoveTableHeapContextFreeSpace(TableHeapContext tableHeapContext) =>
        TryCatch(operation: () =>
    {
        ValidateContextSource(tableHeapContext: tableHeapContext);
        RemoveFreeSpace(pageId: tableHeapContext.PageId);
    });

    public void ClearTableHeapContextFreeSpaces(TableHeapContext tableHeapContext) =>
        TryCatch(operation: () =>
    {
        ValidateContextSource(tableHeapContext: tableHeapContext);
        ClearFreeSpaces();
    });

    private async ValueTask<RowId> InsertCoreAsync(ReadOnlyMemory<byte> row, CancellationToken cancellationToken = default)
    {
        if (row.IsEmpty)
        {
            throw new ArgumentException("Raw heap records cannot be empty.", nameof(row));
        }

        if (row.Length > resources.ReadHeapPageResourceSize() - sql_storage_engine.Models.Heap.HeapPageFormat.HeaderLength - sql_storage_engine.Models.Heap.HeapPageFormat.SlotEntryLength)
        {
            throw new StorageResourceExhaustedException("Row is too large for an empty heap page.");
        }

        var requiredBytes = checked(row.Length + sql_storage_engine.Models.Heap.HeapPageFormat.SlotEntryLength);

        while (FindFreeSpacePage(requiredBytes: requiredBytes)is { } candidate)
        {
            if (!state.KnownPages!.Contains(item: candidate))
            {
                RemoveFreeSpace(pageId: candidate);
                continue;
            }

            var candidatePin = await AcquirePageAsync(pageId: candidate, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                HeapPageLayout candidatePage = BindHeapPageLayout(page: candidatePin.Memory, pageId: candidate);

                if (TryInsertHeapPageLayout(heapPageLayout: candidatePage, row: row.Span, slotId: out var candidateSlot, slotGeneration: out var candidateGeneration))
                {
                    UpdateFreeSpace(pageId: candidate, freeBytes: ReadFreeBytes(heapPageLayout: candidatePage));
                    resources.MarkHeapPageLeaseDirty(heapPageLease: candidatePin, logSequenceNumber: 0);

                    return new sql_storage_engine.Models.Identifiers.RowId
                    {
                        PageId = candidate,
                        SlotId = candidateSlot,
                        Generation = candidateGeneration
                    };
                }

                RemoveFreeSpace(pageId: candidate);
            }
            finally
            {
                resources.ReleaseHeapPageLease(heapPageLease: candidatePin);
            }
        }

        var current = state.RootPageId;

        while (true)
        {
            PageId? next;

            {
                var pin = await AcquirePageAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                try
                {
                    HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: current);
                    state.KnownPages!.Add(item: current);

                    if (TryInsertHeapPageLayout(heapPageLayout: page, row: row.Span, slotId: out var slotId, slotGeneration: out var generation))
                    {
                        UpdateFreeSpace(pageId: current, freeBytes: ReadFreeBytes(heapPageLayout: page));
                        resources.MarkHeapPageLeaseDirty(heapPageLease: pin, logSequenceNumber: 0);

                        return new sql_storage_engine.Models.Identifiers.RowId
                        {
                            PageId = current,
                            SlotId = slotId,
                            Generation = generation
                        };
                    }

                    UpdateFreeSpace(pageId: current, freeBytes: ReadFreeBytes(heapPageLayout: page));
                    next = ReadNextPageId(heapPageLayout: page);
                }
                finally
                {
                    resources.ReleaseHeapPageLease(heapPageLease: pin);
                }
            }

            if (next is { } nextPage)
            {
                current = nextPage;
                continue;
            }

            var allocated = await AllocatePageAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            SlotId insertedSlot;
            SlotGeneration insertedGeneration;

            {
                var newPin = await AcquirePageAsync(pageId: allocated, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                try
                {
                    layouts.InitializeHeapPageLayout(page: newPin.Memory.Span, pageId: (allocated).Value, previousPageId: (current).Value, nextPageId: null);
                    HeapPageLayout newPage = BindHeapPageLayout(page: newPin.Memory, pageId: allocated);

                    if (!TryInsertHeapPageLayout(heapPageLayout: newPage, row: row.Span, slotId: out insertedSlot, slotGeneration: out insertedGeneration))
                    {
                        throw new StorageResourceExhaustedException("Row is too large for an empty heap page.");
                    }

                    resources.MarkHeapPageLeaseDirty(heapPageLease: newPin, logSequenceNumber: 0);
                    UpdateFreeSpace(pageId: allocated, freeBytes: ReadFreeBytes(heapPageLayout: newPage));
                    state.KnownPages!.Add(item: allocated);
                }
                finally
                {
                    resources.ReleaseHeapPageLease(heapPageLease: newPin);
                }
            }

            var tailPin = await AcquirePageAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                HeapPageLayout tail = BindHeapPageLayout(page: tailPin.Memory, pageId: current);
                LinkHeapPageLayout(heapPageLayout: tail, nextPageId: allocated);
                resources.MarkHeapPageLeaseDirty(heapPageLease: tailPin, logSequenceNumber: 0);

                return new sql_storage_engine.Models.Identifiers.RowId
                {
                    PageId = allocated,
                    SlotId = insertedSlot,
                    Generation = insertedGeneration
                };
            }
            finally
            {
                resources.ReleaseHeapPageLease(heapPageLease: tailPin);
            }
        }
    }

    private async ValueTask<bool> DeleteCoreAsync(RowId rowId, CancellationToken cancellationToken = default)
    {
        var located = await FindPageAsync(pageId: rowId.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!located)
        {
            return false;
        }

        var pin = await AcquirePageAsync(pageId: rowId.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: rowId.PageId);

            if (!DeleteHeapPageLayout(heapPageLayout: page, deletedSlotId: rowId.SlotId, deletedSlotGeneration: rowId.Generation))
            {
                return false;
            }

            UpdateFreeSpace(pageId: rowId.PageId, freeBytes: ReadFreeBytes(heapPageLayout: page));
            resources.MarkHeapPageLeaseDirty(heapPageLease: pin, logSequenceNumber: 0);
            return true;
        }
        finally
        {
            resources.ReleaseHeapPageLease(heapPageLease: pin);
        }
    }

    private async ValueTask<HeapUpdateResult> UpdateCoreAsync(RowId rowId, ReadOnlyMemory<byte> row, CancellationToken cancellationToken = default)
    {
        if (!await FindPageAsync(pageId: rowId.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            return HeapUpdateResult.Absent;
        }

        var pin = await AcquirePageAsync(pageId: rowId.PageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: rowId.PageId);
            var result = UpdateHeapPageLayout(heapPageLayout: page, updatedSlotId: rowId.SlotId, updatedSlotGeneration: rowId.Generation, row: row.Span);
            UpdateFreeSpace(pageId: rowId.PageId, freeBytes: ReadFreeBytes(heapPageLayout: page));

            if (result == HeapUpdateResult.Updated)
            {
                resources.MarkHeapPageLeaseDirty(heapPageLease: pin, logSequenceNumber: 0);
            }

            return result;
        }
        finally
        {
            resources.ReleaseHeapPageLease(heapPageLease: pin);
        }
    }

    private async ValueTask<(bool Compacted, int ReclaimedBytes)> CompactPageWithResultCoreAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        if (!await FindPageAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            return (false, 0);
        }

        var pin = await AcquirePageAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: pageId);
            var before = ReadFreeBytes(heapPageLayout: page);
            CompactHeapPageLayout(heapPageLayout: page);
            UpdateFreeSpace(pageId: pageId, freeBytes: ReadFreeBytes(heapPageLayout: page));
            resources.MarkHeapPageLeaseDirty(heapPageLease: pin, logSequenceNumber: 0);
            return (true, ReadFreeBytes(heapPageLayout: page) - before);
        }
        finally
        {
            resources.ReleaseHeapPageLease(heapPageLease: pin);
        }
    }

    private async ValueTask<IReadOnlyList<PageId>> GetPageIdsCoreAsync(int maximumPages, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value: maximumPages);
        var pages = new List<PageId>();
        var current = state.RootPageId;
        var seen = new HashSet<PageId>();

        while (seen.Add(item: current))
        {
            if (pages.Count == maximumPages)
            {
                throw new StorageResourceExhaustedException("Heap maintenance scan-page limit exceeded.");
            }

            pages.Add(item: current);

            var pin = await AcquirePageAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: current);

                if (ReadNextPageId(heapPageLayout: page)is not { } next)
                {
                    return pages.AsReadOnly();
                }

                current = next;
            }
            finally
            {
                resources.ReleaseHeapPageLease(heapPageLease: pin);
            }
        }

        throw new StorageCorruptionException($"Cycle detected in table heap at {current}.");
    }

    private async ValueTask RebuildFreeSpaceMapCoreAsync(CancellationToken cancellationToken = default)
    {
        ClearFreeSpaces();
        state.KnownPages!.Clear();
        var current = state.RootPageId;

        while (state.KnownPages!.Add(item: current))
        {
            var pin = await AcquirePageAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: current);
                UpdateFreeSpace(pageId: current, freeBytes: ReadFreeBytes(heapPageLayout: page));

                if (ReadNextPageId(heapPageLayout: page)is not { } next)
                {
                    return;
                }

                current = next;
            }
            finally
            {
                resources.ReleaseHeapPageLease(heapPageLease: pin);
            }
        }

        throw new StorageCorruptionException($"Cycle detected while rebuilding free-space map at {current}.");
    }

    private async ValueTask<(TableHeapLookupResult Result, ReadOnlyMemory<byte> Row)> ReadCoreAsync(RowId rowId, CancellationToken cancellationToken = default)
    {
        var current = state.RootPageId;
        HashSet<PageId> seen = [];

        while (seen.Add(item: current))
        {
            var pin = await AcquirePageAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: current);

                if (current == rowId.PageId)
                {
                    var result = ReadHeapPageLayoutRow(heapPageLayout: page, slotId: rowId.SlotId, slotGeneration: rowId.Generation, row: out var row);

                    return (result switch
                    {
                        HeapReadResult.Found => TableHeapLookupResult.Found,
                        HeapReadResult.UnknownSlot => TableHeapLookupResult.UnknownSlot,
                        HeapReadResult.Deleted => TableHeapLookupResult.Deleted,
                        HeapReadResult.StaleGeneration => TableHeapLookupResult.StaleGeneration,
                        _ => throw new StorageCorruptionException(message: "Heap page returned an unsupported lookup status.")}, row);
                }

                if (ReadNextPageId(heapPageLayout: page)is not { } next)
                {
                    return (TableHeapLookupResult.UnknownPage, default);
                }

                current = next;
            }
            finally
            {
                resources.ReleaseHeapPageLease(heapPageLease: pin);
            }
        }

        throw new StorageCorruptionException($"Cycle detected in table heap at {current}.");
    }

    private async IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> ScanCoreAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var current = state.RootPageId;
        HashSet<PageId> seen = [];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!seen.Add(item: current))
            {
                throw new StorageCorruptionException($"Cycle detected in table heap at {current}.");
            }

            IReadOnlyList<HeapPageRow> rows;
            PageId? next;

            try
            {
                var pin = await AcquirePageAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                try
                {
                    HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: current);
                    rows = ReadLiveRows(heapPageLayout: page);
                    next = ReadNextPageId(heapPageLayout: page);
                }
                finally
                {
                    resources.ReleaseHeapPageLease(heapPageLease: pin);
                }
            }
            catch (StorageResourceException exception)
            {
                throw new StorageCorruptionException($"Heap chain references inaccessible {current}.", exception);
            }

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return (new sql_storage_engine.Models.Identifiers.RowId { PageId = current, SlotId = row.SlotId, Generation = row.Generation }, row.Bytes);
            }

            if (next is not { } nextPage)
            {
                yield break;
            }

            current = nextPage;
        }
    }

    private async IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> SampleCoreAsync(decimal pagePercentage, long? repeatableSeed = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (pagePercentage is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pagePercentage));
        }

        if (repeatableSeed is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(repeatableSeed));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (pagePercentage == 0)
        {
            yield break;
        }

        var selectionSeed = repeatableSeed is { } seed ? unchecked((ulong)seed) : unchecked((ulong)resources.ReadHeapPageResourceRandomSeed());
        var current = state.RootPageId;
        HashSet<PageId> seen = [];

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!seen.Add(item: current))
            {
                throw new StorageCorruptionException($"Cycle detected in table heap at {current}.");
            }

            IReadOnlyList<HeapPageRow> rows;
            PageId? next;

            try
            {
                var pin = await AcquirePageAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                try
                {
                    HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: current);
                    rows = pagePercentage == 100 || IsSampledPage(pageId: current, percentage: pagePercentage, seed: selectionSeed) ? ReadLiveRows(heapPageLayout: page) : [];
                    next = ReadNextPageId(heapPageLayout: page);
                }
                finally
                {
                    resources.ReleaseHeapPageLease(heapPageLease: pin);
                }
            }
            catch (StorageResourceException exception)
            {
                throw new StorageCorruptionException($"Heap chain references inaccessible {current}.", exception);
            }

            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return (new sql_storage_engine.Models.Identifiers.RowId { PageId = current, SlotId = row.SlotId, Generation = row.Generation }, row.Bytes);
            }

            if (next is not { } nextPage)
            {
                yield break;
            }

            current = nextPage;
        }
    }

    private async ValueTask<long> GetLiveRowCountCoreAsync(CancellationToken cancellationToken = default)
    {
        var current = state.RootPageId;
        HashSet<PageId> seen = [];
        long count = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!seen.Add(item: current))
            {
                throw new StorageCorruptionException($"Cycle detected in table heap at {current}.");
            }

            PageId? next;

            try
            {
                var pin = await AcquirePageAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                try
                {
                    HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: current);
                    count = checked(count + CountLiveRows(heapPageLayout: page));
                    next = ReadNextPageId(heapPageLayout: page);
                }
                finally
                {
                    resources.ReleaseHeapPageLease(heapPageLease: pin);
                }
            }
            catch (StorageResourceException exception)
            {
                throw new StorageCorruptionException($"Heap chain references inaccessible {current}.", exception);
            }

            if (next is not { } nextPage)
            {
                return count;
            }

            current = nextPage;
        }
    }

    private static bool IsSampledPage(PageId pageId, decimal percentage, ulong seed)
    {
        var value = Mix(value: pageId.Value ^ Mix(value: seed + 0x9E3779B97F4A7C15UL));
        var unitInterval = value / ((decimal)ulong.MaxValue + 1m);
        return unitInterval < percentage / 100m;
    }

    private static ulong Mix(ulong value)
    {
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        return value ^ value >> 31;
    }

    private async ValueTask<bool> FindPageAsync(PageId pageId, CancellationToken cancellationToken)
    {
        if (state.KnownPages!.Contains(item: pageId))
        {
            return true;
        }

        var current = state.RootPageId;
        HashSet<PageId> seen = [];

        while (seen.Add(item: current))
        {
            state.KnownPages!.Add(item: current);

            if (current == pageId)
            {
                return true;
            }

            var pin = await AcquirePageAsync(pageId: current, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                HeapPageLayout page = BindHeapPageLayout(page: pin.Memory, pageId: current);

                if (ReadNextPageId(heapPageLayout: page)is not { } next)
                {
                    return false;
                }

                current = next;
            }
            finally
            {
                resources.ReleaseHeapPageLease(heapPageLease: pin);
            }
        }

        throw new StorageCorruptionException($"Cycle detected in table heap at {current}.");
    }

    private HeapPageLayout BindHeapPageLayout(Memory<byte> page, PageId pageId) =>
        layouts.BindHeapPageLayout(page: page, expectedPageId: pageId.Value);

    private int ReadFreeBytes(HeapPageLayout heapPageLayout) =>
        layouts.CountHeapPageLayoutFreeBytes(heapPageLayout: heapPageLayout);

    private PageId? ReadNextPageId(HeapPageLayout heapPageLayout) =>
        layouts.ReadHeapPageLayoutState(heapPageLayout: heapPageLayout)
            .HeapHeader.NextPageId;

    private bool TryInsertHeapPageLayout(HeapPageLayout heapPageLayout, ReadOnlySpan<byte> row, out SlotId slotId, out SlotGeneration slotGeneration)
    {
        var result = layouts.TryInsertHeapPageLayoutRow(heapPageLayout: heapPageLayout, row: row);
        slotId = result.ResultSlotId;
        slotGeneration = result.ResultGeneration;
        return result.Inserted;
    }

    private bool DeleteHeapPageLayout(HeapPageLayout heapPageLayout, SlotId deletedSlotId, SlotGeneration deletedSlotGeneration) =>
        layouts.DeleteHeapPageLayoutRow(deletedHeapPageLayout: heapPageLayout, slotIndex: deletedSlotId.Value, generation: deletedSlotGeneration.Value);

    private HeapUpdateResult UpdateHeapPageLayout(HeapPageLayout heapPageLayout, SlotId updatedSlotId, SlotGeneration updatedSlotGeneration, ReadOnlySpan<byte> row) =>
        layouts.UpdateHeapPageLayoutRow(updatedHeapPageLayout: heapPageLayout, slotIndex: updatedSlotId.Value, generation: updatedSlotGeneration.Value, row: row);

    private void CompactHeapPageLayout(HeapPageLayout heapPageLayout) =>
        layouts.CompactHeapPageLayout(heapPageLayout: heapPageLayout);

    private void LinkHeapPageLayout(HeapPageLayout heapPageLayout, PageId? nextPageId) =>
        layouts.LinkHeapPageLayout(heapPageLayout: heapPageLayout, nextPageId: nextPageId?.Value);

    private HeapReadResult ReadHeapPageLayoutRow(HeapPageLayout heapPageLayout, SlotId slotId, SlotGeneration slotGeneration, out ReadOnlyMemory<byte> row)
    {
        var result = layouts.ReadHeapPageLayoutRow(heapPageLayout: heapPageLayout, slotIndex: slotId.Value, generation: slotGeneration.Value);
        row = result.Row;
        return result.ReadResult;
    }

    private IReadOnlyList<HeapPageRow> ReadLiveRows(HeapPageLayout heapPageLayout) =>
        layouts.ReadHeapPageLayoutLiveRows(heapPageLayout: heapPageLayout)
            .LiveRows;

    private int CountLiveRows(HeapPageLayout heapPageLayout) =>
        layouts.CountHeapPageLayoutLiveRows(heapPageLayout: heapPageLayout);

    private ValueTask<HeapPageLease> AcquirePageAsync(PageId pageId, CancellationToken cancellationToken) =>
        resources.AcquireHeapPageLeaseAsync(pageId: pageId.Value, cancellationToken: cancellationToken);

    private async ValueTask<PageId> AllocatePageAsync(CancellationToken cancellationToken) =>
        new PageId
    {
        Value = await resources.AllocateHeapPageResourceAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
    };

    private PageId? FindFreeSpacePage(int requiredBytes)
    {
        var id = resources.FindHeapPageResource(requiredBytes: requiredBytes);

        return id is null ? null : new PageId
        {
            Value = id.Value
        };
    }

    private void UpdateFreeSpace(PageId pageId, int freeBytes) =>
        resources.UpdateHeapPageResourceFreeSpace(pageId: pageId.Value, freeBytes: freeBytes);

    private void RemoveFreeSpace(PageId pageId) =>
        resources.RemoveHeapPageResourceFreeSpace(pageId: pageId.Value);

    private void ClearFreeSpaces() =>
        resources.ClearHeapPageResourceFreeSpaces();

    private static async IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> GuardRows(IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var iterator = source.GetAsyncEnumerator(cancellationToken: cancellationToken);

        while (await TryCatch(operation: async () => await iterator.MoveNextAsync()
            .ConfigureAwait(continueOnCapturedContext: false))
            .ConfigureAwait(continueOnCapturedContext: false))
        {
            yield return iterator.Current;
        }
    }
}