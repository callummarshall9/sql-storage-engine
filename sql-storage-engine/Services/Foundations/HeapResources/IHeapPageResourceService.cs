// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Heap;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Foundations.HeapResources;
internal interface IHeapPageResourceService
{
    int ReadHeapPageResourceSize();

    ValueTask<byte[]> ReadHeapPageResourceAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask WriteHeapPageResourceAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask<ulong> AllocateHeapPageResourceAsync(CancellationToken cancellationToken);

    ValueTask DiscardHeapPageResourceAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask FreeHeapPageResourceAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask<HeapPageLease> AcquireHeapPageLeaseAsync(ulong pageId, CancellationToken cancellationToken);

    void MarkHeapPageLeaseDirty(HeapPageLease heapPageLease, ulong logSequenceNumber);

    void ReleaseHeapPageLease(HeapPageLease heapPageLease);

    long ReadHeapPageResourceRandomSeed();

    ulong? FindHeapPageResource(int requiredBytes);

    void UpdateHeapPageResourceFreeSpace(ulong pageId, int freeBytes);

    void RemoveHeapPageResourceFreeSpace(ulong pageId);

    void ClearHeapPageResourceFreeSpaces();
}