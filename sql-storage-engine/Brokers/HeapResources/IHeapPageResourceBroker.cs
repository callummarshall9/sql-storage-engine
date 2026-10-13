// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.HeapResources;
internal interface IHeapPageResourceBroker
{
    int ReadPageSize();

    ValueTask<byte[]> ReadAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask WriteAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask<ulong> AllocateAsync(CancellationToken cancellationToken);

    ValueTask DiscardAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask FreeAsync(ulong pageId, CancellationToken cancellationToken);

    ValueTask<(Guid Id, Memory<byte> Memory)> AcquireAsync(ulong pageId, CancellationToken cancellationToken);

    void MarkDirty(Guid leaseId, ulong logSequenceNumber);

    void Release(Guid leaseId);

    long ReadRandomSeed();

    ulong? Find(int requiredBytes);

    void Update(ulong pageId, int freeBytes);

    void Remove(ulong pageId);

    void Clear();
}