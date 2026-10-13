// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Dependencies.HeapResources;

namespace sql_storage_engine.Brokers.HeapResources;
internal sealed class HeapPageResourceBroker : IHeapPageResourceBroker
{
    private readonly HeapPageResourceDependency dependency;
    public HeapPageResourceBroker(HeapPageResourceConfiguration heapPageResourceConfiguration)
    {
        dependency = new HeapPageResourceDependency(pageSize: heapPageResourceConfiguration.PageSize!, pin: heapPageResourceConfiguration.Pin!, allocate: heapPageResourceConfiguration.Allocate!, discard: heapPageResourceConfiguration.Discard!, free: heapPageResourceConfiguration.Free!, find: heapPageResourceConfiguration.Find, update: heapPageResourceConfiguration.Update, remove: heapPageResourceConfiguration.Remove, clear: heapPageResourceConfiguration.Clear);
    }

    public int ReadPageSize() =>
        dependency.ReadPageSize();

    public ValueTask<byte[]> ReadAsync(ulong pageId, CancellationToken cancellationToken) =>
        dependency.ReadAsync(pageId: pageId, cancellationToken: cancellationToken);

    public ValueTask WriteAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        dependency.WriteAsync(pageId: pageId, source: source, cancellationToken: cancellationToken);

    public ValueTask<ulong> AllocateAsync(CancellationToken cancellationToken) =>
        dependency.AllocateAsync(cancellationToken: cancellationToken);

    public ValueTask DiscardAsync(ulong pageId, CancellationToken cancellationToken) =>
        dependency.DiscardAsync(pageId: pageId, cancellationToken: cancellationToken);

    public ValueTask FreeAsync(ulong pageId, CancellationToken cancellationToken) =>
        dependency.FreeAsync(pageId: pageId, cancellationToken: cancellationToken);

    public ValueTask<(Guid Id, Memory<byte> Memory)> AcquireAsync(ulong pageId, CancellationToken cancellationToken) =>
        dependency.AcquireAsync(pageId: pageId, cancellationToken: cancellationToken);

    public void MarkDirty(Guid leaseId, ulong logSequenceNumber) =>
        dependency.MarkDirty(leaseId: leaseId, logSequenceNumber: logSequenceNumber);

    public void Release(Guid leaseId) =>
        dependency.Release(leaseId: leaseId);

    public long ReadRandomSeed() =>
        dependency.ReadRandomSeed();

    public ulong? Find(int requiredBytes) =>
        dependency.Find(requiredBytes: requiredBytes);

    public void Update(ulong pageId, int freeBytes) =>
        dependency.Update(pageId: pageId, freeBytes: freeBytes);

    public void Remove(ulong pageId) =>
        dependency.Remove(pageId: pageId);

    public void Clear() =>
        dependency.Clear();
}