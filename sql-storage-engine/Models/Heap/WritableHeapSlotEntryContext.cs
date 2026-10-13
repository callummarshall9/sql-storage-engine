// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Heap;
internal ref struct WritableHeapSlotEntryContext
{
    public Span<byte> page;
    public ushort slotIndex;
    public HeapSlotEntry heapSlotEntry;
}