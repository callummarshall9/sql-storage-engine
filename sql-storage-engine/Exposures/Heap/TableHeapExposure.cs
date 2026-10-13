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
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Exposures.Buffers;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Coordinations.TableHeaps;

namespace sql_storage_engine.Exposures.Heap;
public sealed class TableHeapExposure : ICompositionExposure, IFreeSpaceMapExposure, ITableHeapExposure
{
    private readonly TableHeapContextCoordinationService service;
    internal TableHeapExposure(TableHeapBinding tableHeapBinding)
    {
        service = (TableHeapContextCoordinationService)tableHeapBinding.Service!;
    }

    public TableHeapExposure(PageId pageId, IBufferPoolExposure bufferPool, IPageAllocatorExposure allocator)
    {
        service = TableHeapContextCoordinationService.CreateTableHeapContextCoordinationService(heapPageResourceConfiguration: new HeapPageResourceConfiguration { RootPageId = pageId.Value, BufferSource = bufferPool, AllocatorSource = allocator, PageSize = () => bufferPool.ReadPageSize(), Pin = (id, token) => bufferPool.GetPageLeaseAsync(pageId: new PageId { Value = id }, cancellationToken: token), Allocate = async token => (await allocator.AllocateAsync(pageType: PageType.Heap, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Value, Discard = (id, token) => bufferPool.DiscardPageAsync(pageId: new PageId { Value = id }, cancellationToken: token), Free = (id, token) => allocator.FreeAsync(pageId: new PageId { Value = id }, cancellationToken: token) });
    }

    public TableHeapExposure(PageId pageId, IBufferPoolExposure bufferPool, IPageAllocatorExposure allocator, IFreeSpaceMapExposure freeSpaceMap)
    {
        service = TableHeapContextCoordinationService.CreateTableHeapContextCoordinationService(heapPageResourceConfiguration: new HeapPageResourceConfiguration { FreeSpaceRequired = true, RootPageId = pageId.Value, BufferSource = bufferPool, AllocatorSource = allocator, PageSize = () => bufferPool.ReadPageSize(), Pin = (id, token) => bufferPool.GetPageLeaseAsync(pageId: new PageId { Value = id }, cancellationToken: token), Allocate = async token => (await allocator.AllocateAsync(pageType: PageType.Heap, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Value, Discard = (id, token) => bufferPool.DiscardPageAsync(pageId: new PageId { Value = id }, cancellationToken: token), Free = (id, token) => allocator.FreeAsync(pageId: new PageId { Value = id }, cancellationToken: token), FreeSpaceSource = freeSpaceMap, Find = requiredBytes => freeSpaceMap!.FindPage(requiredBytes: requiredBytes)?.Value, Update = (id, bytes) => freeSpaceMap!.Update(updatedPageId: new PageId { Value = id }, freeBytes: bytes), Remove = id => freeSpaceMap!.Remove(pageId: new PageId { Value = id }), Clear = () => freeSpaceMap!.Clear() });
    }

    public static ValueTask<TableHeapExposure> CreateTableHeapExposureAsync(IBufferPoolExposure bufferPool, IPageAllocatorExposure allocator, CancellationToken cancellationToken = default) =>
        new TableHeapExposure(pageId: default, bufferPool: bufferPool, allocator: allocator).CreateTableHeapExposureSourceAsync(cancellationToken: cancellationToken);

    public static ValueTask<TableHeapExposure> CreateTableHeapExposureAsync(IBufferPoolExposure bufferPool, IPageAllocatorExposure allocator, IFreeSpaceMapExposure freeSpaceMap, CancellationToken cancellationToken = default) =>
        new TableHeapExposure(pageId: default, bufferPool: bufferPool, allocator: allocator, freeSpaceMap: freeSpaceMap).CreateTableHeapExposureSourceAsync(cancellationToken: cancellationToken);

    private async ValueTask<TableHeapExposure> CreateTableHeapExposureSourceAsync(CancellationToken cancellationToken)
    {
        await service.CreateTableHeapContextAsync(tableHeapContext: new TableHeapContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return this;
    }

    public static ValueTask<TableHeapExposure> OpenAsync(PageId pageId, IBufferPoolExposure bufferPool, IPageAllocatorExposure allocator, CancellationToken cancellationToken = default) =>
        new TableHeapExposure(pageId: pageId, bufferPool: bufferPool, allocator: allocator).OpenSourceAsync(cancellationToken: cancellationToken);

    public static ValueTask<TableHeapExposure> OpenAsync(PageId pageId, IBufferPoolExposure bufferPool, IPageAllocatorExposure allocator, IFreeSpaceMapExposure freeSpaceMap, CancellationToken cancellationToken = default) =>
        new TableHeapExposure(pageId: pageId, bufferPool: bufferPool, allocator: allocator, freeSpaceMap: freeSpaceMap).OpenSourceAsync(cancellationToken: cancellationToken);

    private async ValueTask<TableHeapExposure> OpenSourceAsync(CancellationToken cancellationToken)
    {
        await service.OpenTableHeapContextAsync(tableHeapContext: new TableHeapContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return this;
    }

    public PageId ReadRootPageId() =>
        service.ReadTableHeapContext(tableHeapContext: new TableHeapContext())
            .RootPageId;

    public IFreeSpaceMapExposure ReadFreeSpaceMap() =>
        this;

    public async ValueTask<RowId> InsertAsync(ReadOnlyMemory<byte> row, CancellationToken cancellationToken = default) =>
        (await service.AddTableHeapContextAsync(newTableHeapContext: new TableHeapContext { Row = row }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).ResultRowId;

    public async ValueTask<bool> DeleteAsync(RowId deletedRowId, CancellationToken cancellationToken = default) =>
        (await service.DeleteTableHeapContextAsync(deletedTableHeapContext: new TableHeapContext { RowId = deletedRowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Result;

    public async ValueTask<HeapUpdateResult> UpdateAsync(RowId updatedRowId, ReadOnlyMemory<byte> row, CancellationToken cancellationToken = default) =>
        (await service.UpdateTableHeapContextAsync(updatedTableHeapContext: new TableHeapContext { RowId = updatedRowId, Row = row }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).UpdateResult;

    public async ValueTask<bool> CompactPageAsync(PageId pageId, CancellationToken cancellationToken = default) =>
        (await service.CompactTableHeapContextAsync(tableHeapContext: new TableHeapContext { PageId = pageId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Result;

    public async ValueTask<(bool Compacted, int ReclaimedBytes)> CompactPageWithResultAsync(PageId pageId, CancellationToken cancellationToken = default)
    {
        var result = await service.CompactTableHeapContextAsync(tableHeapContext: new TableHeapContext { PageId = pageId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return (result.Result, result.ReclaimedBytes);
    }

    public async ValueTask<IReadOnlyList<PageId>> GetPageIdsAsync(int maximumPages, CancellationToken cancellationToken = default) =>
        (await service.ReadTableHeapContextPageIdsAsync(tableHeapContext: new TableHeapContext { MaximumPages = maximumPages }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PageIds!;

    public ValueTask RebuildFreeSpaceMapAsync(CancellationToken cancellationToken = default) =>
        service.RebuildTableHeapContextFreeSpacesAsync(tableHeapContext: new TableHeapContext(), cancellationToken: cancellationToken);

    public async ValueTask<(TableHeapLookupResult Result, ReadOnlyMemory<byte> Row)> ReadAsync(RowId rowId, CancellationToken cancellationToken = default)
    {
        var result = await service.ReadTableHeapContextRowAsync(tableHeapContext: new TableHeapContext { RowId = rowId }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return (result.LookupResult, result.Row);
    }

    public IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> ScanAsync(CancellationToken cancellationToken = default) =>
        service.ReadTableHeapContextScan(tableHeapContext: new TableHeapContext(), cancellationToken: cancellationToken)
            .Rows!;

    public IAsyncEnumerable<(RowId RowId, ReadOnlyMemory<byte> Row)> SampleAsync(decimal pagePercentage, long? repeatableSeed = null, CancellationToken cancellationToken = default) =>
        service.ReadTableHeapContextSample(tableHeapContext: new TableHeapContext { PagePercentage = pagePercentage, RepeatableSeed = repeatableSeed }, cancellationToken: cancellationToken)
            .Rows!;

    public async ValueTask<long> GetLiveRowCountAsync(CancellationToken cancellationToken = default) =>
        (await service.ReadTableHeapContextLiveCountAsync(tableHeapContext: new TableHeapContext(), cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).LiveRowCount;

    PageId? IFreeSpaceMapExposure.FindPage(int requiredBytes) =>
        service.FindTableHeapContextFreeSpacePage(tableHeapContext: new TableHeapContext { RequiredBytes = requiredBytes })
            .ResultPageId;

    void IFreeSpaceMapExposure.Update(PageId updatedPageId, int freeBytes) =>
        service.SetTableHeapContextFreeSpace(tableHeapContext: new TableHeapContext { PageId = updatedPageId }, freeBytes: freeBytes);

    void IFreeSpaceMapExposure.Remove(PageId pageId) =>
        service.RemoveTableHeapContextFreeSpace(tableHeapContext: new TableHeapContext { PageId = pageId });

    void IFreeSpaceMapExposure.Clear() =>
        service.ClearTableHeapContextFreeSpaces(tableHeapContext: new TableHeapContext());

    public TableHeapBinding ReadTableHeapBinding() =>
        new TableHeapBinding
    {
        Service = service
    };
}