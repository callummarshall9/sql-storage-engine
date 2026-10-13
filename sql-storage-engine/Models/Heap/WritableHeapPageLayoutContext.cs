// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Heap;
internal ref struct WritableHeapPageLayoutContext
{
    public Span<byte> page;
    public HeapPageLayout heapPageLayout;
    public ulong pageId;
    public ulong? previousPageId;
    public ulong? nextPageId;
    public ushort slotIndex;
}