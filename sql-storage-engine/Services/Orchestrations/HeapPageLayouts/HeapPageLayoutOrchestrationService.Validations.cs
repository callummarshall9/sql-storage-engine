// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;

namespace sql_storage_engine.Services.Orchestrations.HeapPageLayouts;
internal sealed partial class HeapPageLayoutOrchestrationService
{
    private static void ValidateHeapPageLayoutSource(ReadOnlySpan<byte> page, ulong? expectedPageId)
    {
        ValidatePageLength(pageLength: page.Length);
        var common = commonHeaders.ReadPageHeader(source: page);
        commonHeaders.CheckPageHeader(pageHeader: common, expectedPageId: expectedPageId ?? common.PageId.Value, expectedType: PageType.Heap);
    }

    private static void ValidateHeapPageLayoutOnInitialize(Span<byte> page, ulong pageId, ulong? previousPageId, ulong? nextPageId)
    {
        ValidatePageLength(pageLength: page.Length);
        Validate(inputs: [(previousPageId == pageId || nextPageId == pageId || previousPageId is not null && previousPageId == nextPageId, "Heap links cannot refer to self or duplicate neighbors.")]);
    }

    private static void ValidateHeapPageLayoutSlotSource(ReadOnlySpan<byte> page, ushort slotIndex)
    {
        ValidatePageLength(pageLength: page.Length);
        ValidateHeapPageLayoutSlotIndex(slotIndex: slotIndex);
    }

    private static void ValidateHeapPageLayoutHeaderOnWrite(Span<byte> page, HeapPageLayout heapPageLayout)
    {
        ValidatePageLength(pageLength: page.Length);
        Validate(inputs: [(heapPageLayout.HeapHeader.SlotDirectoryEnd != (uint)(HeapPageFormat.HeaderLength + heapPageLayout.HeapHeader.SlotCount * HeapPageFormat.SlotEntryLength) || heapPageLayout.HeapHeader.SlotDirectoryEnd > heapPageLayout.HeapHeader.RowDataStart || heapPageLayout.HeapHeader.RowDataStart > page.Length, "Invalid heap header boundaries.")]);
    }

    private static void ValidateHeapPageLayoutSlotOnWrite(Span<byte> page, ushort slotIndex, HeapPageLayout heapPageLayout)
    {
        ValidatePageLength(pageLength: page.Length);
        Validate(inputs: [(!Enum.IsDefined(value: heapPageLayout.Slot.State), "Invalid heap slot state."), ((ulong)HeapPageFormat.HeaderLength + ((ulong)slotIndex + 1) * HeapPageFormat.SlotEntryLength > (ulong)page.Length, "Slot directory exceeds the page.")]);
    }

    private static void ValidateHeapPageLayoutSlotIndex(ushort slotIndex) =>
        Validate(inputs: [((ulong)HeapPageFormat.HeaderLength + ((ulong)slotIndex + 1) * HeapPageFormat.SlotEntryLength > (ulong)PageConstants.MaximumSize, "Slot index exceeds the largest supported page.")]);

    private static void ValidatePageLength(int pageLength) =>
        Validate(inputs: [(!Enum.IsDefined(value: (PageSize)pageLength), "Heap pages require a supported complete size.")]);

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: input.Message);
            }
        }
    }

    private static void ValidateHeapPageLayoutBinding(Memory<byte> page, ulong? expectedPageId) =>
        ValidateHeapPageLayoutSource(page: page.Span, expectedPageId: expectedPageId);

    private static void ValidateBoundHeapPageLayout(HeapPageLayout heapPageLayout) =>
        ValidateHeapPageLayoutSource(page: heapPageLayout.Page.Span, expectedPageId: heapPageLayout.CommonHeader.PageId.Value);

    private static void ValidateHeapPageLayoutRowOnInsert(HeapPageLayout heapPageLayout, ReadOnlySpan<byte> row)
    {
        ValidateBoundHeapPageLayout(heapPageLayout: heapPageLayout);
        Validate(inputs: [(row.IsEmpty, "Raw heap records cannot be empty.")]);
    }

    private static void ValidateHeapPageLayoutSelection(HeapPageLayout heapPageLayout, ushort slotIndex, uint generation)
    {
        ValidateBoundHeapPageLayout(heapPageLayout: heapPageLayout);
    }

    private static void ValidateHeapPageLayoutRowOnUpdate(HeapPageLayout heapPageLayout, ushort slotIndex, uint generation, ReadOnlySpan<byte> row)
    {
        ValidateBoundHeapPageLayout(heapPageLayout: heapPageLayout);
        Validate(inputs: [(row.IsEmpty, "Raw heap records cannot be empty.")]);
    }

    private static void ValidateHeapPageLayoutLink(HeapPageLayout heapPageLayout, ulong? nextPageId)
    {
        ValidateBoundHeapPageLayout(heapPageLayout: heapPageLayout);
        Validate(inputs: [(nextPageId == heapPageLayout.CommonHeader.PageId.Value, "A heap page cannot link to itself.")]);
    }

    private static void ValidateHeapPageLayoutRowOnDelete(HeapPageLayout heapPageLayout, ushort slotIndex, uint generation)
    {
        Validate(inputs: [(!Enum.IsDefined(value: (PageSize)heapPageLayout.Page.Length), "Invalid bound heap page size.")]);
        ValidateHeapPageLayoutSelection(heapPageLayout: heapPageLayout, slotIndex: slotIndex, generation: generation);
    }
}