// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Heap;

namespace sql_storage_engine.Services.Foundations.HeapSlotEntries;
internal sealed partial class HeapSlotEntryService
{
    private static void ValidateHeapSlotEntryIndex(ushort slotIndex) =>
        ValidateArguments(inputs: [((ulong)HeapPageFormat.HeaderLength + ((ulong)slotIndex + 1) * HeapPageFormat.SlotEntryLength > (ulong)PageConstants.MaximumSize, "Slot index exceeds the largest supported page.")]);

    private static void ValidateHeapSlotEntrySource(ReadOnlySpan<byte> page, ushort slotIndex, ushort slotCount, uint rowDataStart) =>
        ValidateArguments(inputs: [(!Enum.IsDefined(value: (PageSize)page.Length), "Unsupported heap page size."), (slotIndex >= slotCount, "Slot is outside the directory."), ((ulong)HeapPageFormat.HeaderLength + ((ulong)slotIndex + 1) * HeapPageFormat.SlotEntryLength > (ulong)page.Length, "Slot directory exceeds the page."), (rowDataStart > page.Length, "Row boundary exceeds the page.")]);

    private static void ValidateHeapSlotEntryOnWrite(Span<byte> page, ushort slotIndex, HeapSlotEntry heapSlotEntry)
    {
        ValidateArguments(inputs: [(!Enum.IsDefined(value: (PageSize)page.Length), "Unsupported heap page size."), ((ulong)HeapPageFormat.HeaderLength + ((ulong)slotIndex + 1) * HeapPageFormat.SlotEntryLength > (ulong)page.Length, "Slot directory exceeds the page."), (!Enum.IsDefined(value: heapSlotEntry.State), "Unsupported slot state.")]);
        ValidateArguments(inputs: [(heapSlotEntry.State == HeapSlotState.Live ? heapSlotEntry.Length == 0 || heapSlotEntry.Offset < HeapPageFormat.HeaderLength || (ulong)heapSlotEntry.Offset + heapSlotEntry.Length > (ulong)page.Length : heapSlotEntry.Offset != 0 || heapSlotEntry.Length != 0, "Slot has an invalid row range.")]);
    }

    private static void ValidateArguments(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: input.Message);
            }
        }
    }
}