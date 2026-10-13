// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Heap;
using sql_storage_engine.Services.Orchestrations.HeapPageLayouts;

namespace sql_storage_engine.Exposures.Heap;
public sealed class HeapPageExposure
{
    private static readonly IHeapPageLayoutOrchestrationService service = new HeapPageLayoutOrchestrationService();
    private readonly HeapPageLayout state;
    private HeapPageExposure(HeapPageLayout heapPageLayout)
    {
        state = heapPageLayout;
    }

    public static HeapPageExposure BindHeapPage(Memory<byte> page, PageId? pageId = null) =>
        new HeapPageExposure(heapPageLayout: service.BindHeapPageLayout(page: page, expectedPageId: pageId?.Value));

    public PageId GetPageId() =>
        service.ReadHeapPageLayoutState(heapPageLayout: state)
            .CommonHeader.PageId;

    public HeapPageHeader ReadHeader() =>
        service.ReadHeapPageLayoutState(heapPageLayout: state)
            .HeapHeader;

    public PageId? ReadNextPageId() =>
        service.ReadHeapPageLayoutState(heapPageLayout: state)
            .HeapHeader.NextPageId;

    public int GetFreeBytes() =>
        service.CountHeapPageLayoutFreeBytes(heapPageLayout: state);

    internal int GetLiveRowCount() =>
        service.CountHeapPageLayoutLiveRows(heapPageLayout: state);

    public bool TryInsert(ReadOnlySpan<byte> row, out SlotId slotId, out SlotGeneration slotGeneration)
    {
        var result = service.TryInsertHeapPageLayoutRow(heapPageLayout: state, row: row);
        slotId = result.ResultSlotId;
        slotGeneration = result.ResultGeneration;
        return result.Inserted;
    }

    public bool Delete(SlotId deletedSlotId, SlotGeneration deletedSlotGeneration) =>
        service.DeleteHeapPageLayoutRow(deletedHeapPageLayout: state, slotIndex: deletedSlotId.Value, generation: deletedSlotGeneration.Value);

    public HeapUpdateResult Update(SlotId updatedSlotId, SlotGeneration updatedSlotGeneration, ReadOnlySpan<byte> row) =>
        service.UpdateHeapPageLayoutRow(updatedHeapPageLayout: state, slotIndex: updatedSlotId.Value, generation: updatedSlotGeneration.Value, row: row);

    public void Compact() =>
        service.CompactHeapPageLayout(heapPageLayout: state);

    public bool TryRead(SlotId slotId, SlotGeneration slotGeneration, out ReadOnlyMemory<byte> row)
    {
        var result = service.ReadHeapPageLayoutRow(heapPageLayout: state, slotIndex: slotId.Value, generation: slotGeneration.Value);
        row = result.Row;
        return result.ReadResult == HeapReadResult.Found;
    }

    public HeapReadResult Read(SlotId slotId, SlotGeneration slotGeneration, out ReadOnlyMemory<byte> row)
    {
        var result = service.ReadHeapPageLayoutRow(heapPageLayout: state, slotIndex: slotId.Value, generation: slotGeneration.Value);
        row = result.Row;
        return result.ReadResult;
    }

    internal void SetNextPage(PageId? nextPageId) =>
        service.LinkHeapPageLayout(heapPageLayout: state, nextPageId: nextPageId?.Value);

    public IReadOnlyList<HeapPageRow> ReadLiveRows() =>
        service.ReadHeapPageLayoutLiveRows(heapPageLayout: state)
            .LiveRows;
}