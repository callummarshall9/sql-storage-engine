// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Brokers.HeapResources;

namespace sql_storage_engine.Services.Foundations.HeapResources;
internal sealed partial class HeapPageResourceService : IHeapPageResourceService
{
    private readonly HeapPageResourceBroker broker;
    public HeapPageResourceService(HeapPageResourceConfiguration heapPageResourceConfiguration)
    {
        broker = OpenHeapPageResourceBroker(heapPageResourceConfiguration: heapPageResourceConfiguration);
    }

    private static HeapPageResourceBroker OpenHeapPageResourceBroker(HeapPageResourceConfiguration heapPageResourceConfiguration) =>
        TryCatch(operation: () =>
    {
        ValidateConfiguration(heapPageResourceConfiguration: heapPageResourceConfiguration);
        return new HeapPageResourceBroker(heapPageResourceConfiguration: heapPageResourceConfiguration);
    });

    public int ReadHeapPageResourceSize() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return broker.ReadPageSize();
    });

    public ValueTask<byte[]> ReadHeapPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        return await broker.ReadAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WriteHeapPageResourceAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(pageId: pageId, source: source, cancellationToken: cancellationToken);

        await broker.WriteAsync(pageId: pageId, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<ulong> AllocateHeapPageResourceAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        return await broker.AllocateAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask DiscardHeapPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        await broker.DiscardAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FreeHeapPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        await broker.FreeAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<HeapPageLease> AcquireHeapPageLeaseAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        var acquired = await broker.AcquireAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new HeapPageLease
        {
            Id = acquired.Id,
            Memory = acquired.Memory
        };
    });

    public void MarkHeapPageLeaseDirty(HeapPageLease heapPageLease, ulong logSequenceNumber) =>
        TryCatch(operation: () =>
    {
        ValidateLeaseMark(heapPageLease: heapPageLease, logSequenceNumber: logSequenceNumber);
        broker.MarkDirty(leaseId: heapPageLease.Id, logSequenceNumber: logSequenceNumber);
    });

    public void ReleaseHeapPageLease(HeapPageLease heapPageLease) =>
        TryCatch(operation: () =>
    {
        ValidateLease(heapPageLease: heapPageLease);
        broker.Release(leaseId: heapPageLease.Id);
    });

    public long ReadHeapPageResourceRandomSeed() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return broker.ReadRandomSeed();
    });

    public ulong? FindHeapPageResource(int requiredBytes) =>
        TryCatch(operation: () =>
    {
        ValidateRequiredBytes(requiredBytes: requiredBytes);
        return broker.Find(requiredBytes: requiredBytes);
    });

    public void UpdateHeapPageResourceFreeSpace(ulong pageId, int freeBytes) =>
        TryCatch(operation: () =>
    {
        ValidateHeapPageResourceFreeSpaceOnUpdate(pageId: pageId, freeBytes: freeBytes);
        broker.Update(pageId: pageId, freeBytes: freeBytes);
    });

    public void RemoveHeapPageResourceFreeSpace(ulong pageId) =>
        TryCatch(operation: () =>
    {
        ValidateFreeSpacePage(pageId: pageId);
        broker.Remove(pageId: pageId);
    });

    public void ClearHeapPageResourceFreeSpaces() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        broker.Clear();
    });
}