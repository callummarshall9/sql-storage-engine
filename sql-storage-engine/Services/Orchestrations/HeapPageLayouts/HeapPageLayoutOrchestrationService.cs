// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Services.Foundations.PageHeaders;
using sql_storage_engine.Services.Foundations.HeapPageHeaders;
using sql_storage_engine.Services.Foundations.HeapSlotEntries;

namespace sql_storage_engine.Services.Orchestrations.HeapPageLayouts;
internal sealed partial class HeapPageLayoutOrchestrationService : IHeapPageLayoutOrchestrationService
{
    private static readonly IPageHeaderService commonHeaders = new PageHeaderService();
    private static readonly IHeapPageHeaderService heapHeaders = new HeapPageHeaderService();
    private static readonly IHeapSlotEntryService slots = new HeapSlotEntryService();
    public void InitializeHeapPageLayout(Span<byte> page, ulong pageId, ulong? previousPageId, ulong? nextPageId) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapPageLayoutOnInitialize(page: context.page, pageId: context.pageId, previousPageId: context.previousPageId, nextPageId: context.nextPageId);
        context.page.Clear();
        commonHeaders.WritePageHeader(destination: context.page, pageHeader: new PageHeader { PageId = new PageId { Value = context.pageId }, PageType = PageType.Heap, FormatVersion = new PageFormatVersion { Value = PageHeaderFormat.CurrentVersion }, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32 });
        heapHeaders.WriteHeapPageHeader(page: context.page, heapPageHeader: new HeapPageHeader { PreviousPageId = context.previousPageId is null ? null : new PageId { Value = context.previousPageId.Value }, NextPageId = context.nextPageId is null ? null : new PageId { Value = context.nextPageId.Value }, SlotDirectoryEnd = HeapPageFormat.HeaderLength, RowDataStart = (uint)context.page.Length });
        commonHeaders.WriteChecksum(page: context.page, pageSize: context.page.Length);
    }, context: new WritableHeapPageLayoutContext { page = page, pageId = pageId, previousPageId = previousPageId, nextPageId = nextPageId });

    public HeapPageLayout ReadHeapPageLayout(ReadOnlySpan<byte> page, ulong? expectedPageId) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapPageLayoutSource(page: context.page, expectedPageId: context.expectedPageId);
        return ReadCore(page: context.page, expectedPageId: context.expectedPageId);
    }, context: new ReadableHeapPageLayoutContext { page = page, expectedPageId = expectedPageId });

    public HeapPageLayout ReadHeapPageLayoutSlot(ReadOnlySpan<byte> page, ushort slotIndex) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapPageLayoutSlotSource(page: context.page, slotIndex: context.slotIndex);
        var layout = ReadCore(page: context.page, expectedPageId: null);
        var slot = slots.ReadHeapSlotEntry(page: context.page, slotIndex: context.slotIndex, slotCount: layout.HeapHeader.SlotCount, rowDataStart: layout.HeapHeader.RowDataStart);

        return layout with
        {
            Slot = slot,
            SlotIndex = context.slotIndex,
            HasSlot = true
        };
    }, context: new ReadableHeapPageLayoutContext { page = page, slotIndex = slotIndex });

    public void WriteHeapPageLayoutHeader(Span<byte> page, HeapPageLayout heapPageLayout) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapPageLayoutHeaderOnWrite(page: context.page, heapPageLayout: context.heapPageLayout);
        heapHeaders.WriteHeapPageHeader(page: context.page, heapPageHeader: context.heapPageLayout.HeapHeader);
    }, context: new WritableHeapPageLayoutContext { page = page, heapPageLayout = heapPageLayout });

    public void WriteHeapPageLayoutSlot(Span<byte> page, ushort slotIndex, HeapPageLayout heapPageLayout) =>
        TryCatch(operation: static context =>
    {
        ValidateHeapPageLayoutSlotOnWrite(page: context.page, slotIndex: context.slotIndex, heapPageLayout: context.heapPageLayout);
        slots.WriteHeapSlotEntry(page: context.page, slotIndex: context.slotIndex, heapSlotEntry: context.heapPageLayout.Slot);
    }, context: new WritableHeapPageLayoutContext { page = page, slotIndex = slotIndex, heapPageLayout = heapPageLayout });

    public int CalculateHeapPageLayoutSlotOffset(ushort slotIndex) =>
        TryCatch(operation: () =>
    {
        ValidateHeapPageLayoutSlotIndex(slotIndex: slotIndex);
        return slots.CalculateHeapSlotEntryOffset(slotIndex: slotIndex);
    });

    private static HeapPageLayout ReadCore(ReadOnlySpan<byte> page, ulong? expectedPageId)
    {
        var common = commonHeaders.ReadPageHeader(source: page);
        commonHeaders.CheckPageHeader(pageHeader: common, expectedPageId: expectedPageId ?? common.PageId.Value, expectedType: PageType.Heap);
        var header = heapHeaders.ReadHeapPageHeader(page: page);

        for (ushort slotIndex = 0; slotIndex < header.SlotCount; slotIndex++)
        {
            _ = slots.ReadHeapSlotEntry(page: page, slotIndex: slotIndex, slotCount: header.SlotCount, rowDataStart: header.RowDataStart);
        }

        return new HeapPageLayout
        {
            CommonHeader = common,
            HeapHeader = header
        };
    }
}