// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;

namespace sql_storage_engine.Services.Foundations.HeapSlotEntries;
internal interface IHeapSlotEntryService
{
    HeapSlotEntry ReadHeapSlotEntry(ReadOnlySpan<byte> page, ushort slotIndex, ushort slotCount, uint rowDataStart);

    void WriteHeapSlotEntry(Span<byte> page, ushort slotIndex, HeapSlotEntry heapSlotEntry);

    int CalculateHeapSlotEntryOffset(ushort slotIndex);
}