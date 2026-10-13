// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Heap;
internal ref struct ReadableHeapSlotEntryContext
{
    public ReadOnlySpan<byte> page;
    public ushort slotIndex;
    public ushort slotCount;
    public uint rowDataStart;
}