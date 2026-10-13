// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Heap;
internal ref struct ReadableHeapPageLayoutContext
{
    public ReadOnlySpan<byte> page;
    public ulong? expectedPageId;
    public ushort slotIndex;
}