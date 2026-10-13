// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Heap;

namespace sql_storage_engine.Services.Orchestrations.HeapResources;
internal interface IHeapPageResourceOrchestrationService
{
    ValueTask<HeapPageLease> AcquireHeapPageLeaseAsync(ulong pageId, CancellationToken cancellationToken);

    void MarkHeapPageLeaseDirty(HeapPageLease heapPageLease, ulong logSequenceNumber);

    void ReleaseHeapPageLease(HeapPageLease heapPageLease);

    long ReadHeapPageResourceRandomSeed();

    ulong? FindHeapPageResource(int requiredBytes);

    void UpdateHeapPageResourceFreeSpace(ulong pageId, int freeBytes);

    void RemoveHeapPageResourceFreeSpace(ulong pageId);

    void ClearHeapPageResourceFreeSpaces();

    int ReadHeapPageResourceSize();

    ValueTask<ulong> AllocateHeapPageResourceAsync(CancellationToken cancellationToken);
}