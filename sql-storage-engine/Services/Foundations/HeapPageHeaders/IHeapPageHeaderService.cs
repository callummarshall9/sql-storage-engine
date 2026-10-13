// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;

namespace sql_storage_engine.Services.Foundations.HeapPageHeaders;
internal interface IHeapPageHeaderService
{
    HeapPageHeader ReadHeapPageHeader(ReadOnlySpan<byte> page);

    void WriteHeapPageHeader(Span<byte> page, HeapPageHeader heapPageHeader);
}