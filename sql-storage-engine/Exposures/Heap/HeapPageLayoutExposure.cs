// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Services.Orchestrations.HeapPageLayouts;

namespace sql_storage_engine.Exposures.Heap;
public static class HeapPageLayoutExposure
{
    private static readonly IHeapPageLayoutOrchestrationService service = new HeapPageLayoutOrchestrationService();
    public static void InitializeHeapPageLayout(Span<byte> page, ulong pageId, ulong? previousPageId = null, ulong? nextPageId = null) =>
        service.InitializeHeapPageLayout(page: page, pageId: pageId, previousPageId: previousPageId, nextPageId: nextPageId);

    public static HeapPageHeader ReadHeapPageHeader(ReadOnlySpan<byte> page, ulong? expectedPageId = null) =>
        service.ReadHeapPageLayout(page: page, expectedPageId: expectedPageId)
            .HeapHeader;

    public static HeapSlotEntry ReadHeapSlotEntry(ReadOnlySpan<byte> page, ushort slotIndex) =>
        service.ReadHeapPageLayoutSlot(page: page, slotIndex: slotIndex)
            .Slot;

    public static void WriteHeapPageHeader(Span<byte> page, HeapPageHeader heapPageHeader) =>
        service.WriteHeapPageLayoutHeader(page: page, heapPageLayout: new HeapPageLayout { HeapHeader = heapPageHeader });

    public static void WriteHeapSlotEntry(Span<byte> page, ushort slotIndex, HeapSlotEntry heapSlotEntry) =>
        service.WriteHeapPageLayoutSlot(page: page, slotIndex: slotIndex, heapPageLayout: new HeapPageLayout { Slot = heapSlotEntry, SlotIndex = slotIndex, HasSlot = true });

    public static int CalculateHeapSlotEntryOffset(ushort slotIndex) =>
        service.CalculateHeapPageLayoutSlotOffset(slotIndex: slotIndex);

    public static void DecodeFuzzInput(System.ReadOnlyMemory<byte> source) =>
        service.ReadHeapPageLayout(page: source.Span, expectedPageId: null);
}