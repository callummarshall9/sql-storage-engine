// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Heap;

namespace sql_storage_engine.Services.Orchestrations.HeapPageLayouts;
internal interface IHeapPageLayoutOrchestrationService
{
    void InitializeHeapPageLayout(Span<byte> page, ulong pageId, ulong? previousPageId, ulong? nextPageId);

    HeapPageLayout ReadHeapPageLayout(ReadOnlySpan<byte> page, ulong? expectedPageId);

    HeapPageLayout ReadHeapPageLayoutSlot(ReadOnlySpan<byte> page, ushort slotIndex);

    void WriteHeapPageLayoutHeader(Span<byte> page, HeapPageLayout heapPageLayout);

    void WriteHeapPageLayoutSlot(Span<byte> page, ushort slotIndex, HeapPageLayout heapPageLayout);

    int CalculateHeapPageLayoutSlotOffset(ushort slotIndex);

    HeapPageLayout BindHeapPageLayout(Memory<byte> page, ulong? expectedPageId);

    HeapPageLayout ReadHeapPageLayoutState(HeapPageLayout heapPageLayout);

    int CountHeapPageLayoutFreeBytes(HeapPageLayout heapPageLayout);

    int CountHeapPageLayoutLiveRows(HeapPageLayout heapPageLayout);

    HeapPageLayout TryInsertHeapPageLayoutRow(HeapPageLayout heapPageLayout, ReadOnlySpan<byte> row);

    bool DeleteHeapPageLayoutRow(HeapPageLayout deletedHeapPageLayout, ushort slotIndex, uint generation);

    HeapUpdateResult UpdateHeapPageLayoutRow(HeapPageLayout updatedHeapPageLayout, ushort slotIndex, uint generation, ReadOnlySpan<byte> row);

    void CompactHeapPageLayout(HeapPageLayout heapPageLayout);

    HeapPageLayout ReadHeapPageLayoutRow(HeapPageLayout heapPageLayout, ushort slotIndex, uint generation);

    void LinkHeapPageLayout(HeapPageLayout heapPageLayout, ulong? nextPageId);

    HeapPageLayout ReadHeapPageLayoutLiveRows(HeapPageLayout heapPageLayout);
}