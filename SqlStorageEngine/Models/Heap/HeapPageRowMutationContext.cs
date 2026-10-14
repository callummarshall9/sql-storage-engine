// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Heap;
internal ref struct HeapPageRowMutationContext
{
    public HeapPageLayout heapPageLayout;
    public HeapPageLayout updatedHeapPageLayout;
    public ReadOnlySpan<byte> row;
    public ushort slotIndex;
    public uint generation;
}