// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Models.Heap;
internal ref struct ReadableHeapPageHeaderContext
{
    public ReadOnlySpan<byte> page;
}