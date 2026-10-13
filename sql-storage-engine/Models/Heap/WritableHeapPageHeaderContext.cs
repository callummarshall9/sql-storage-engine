// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Heap;
internal ref struct WritableHeapPageHeaderContext
{
    public Span<byte> page;
    public HeapPageHeader heapPageHeader;
}