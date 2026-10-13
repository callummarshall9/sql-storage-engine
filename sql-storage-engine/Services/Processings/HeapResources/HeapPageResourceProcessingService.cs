// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Services.Foundations.HeapResources;

namespace sql_storage_engine.Services.Processings.HeapResources;
internal sealed partial class HeapPageResourceProcessingService(HeapPageResourceService pages) : IHeapPageResourceProcessingService
{
    internal static HeapPageResourceProcessingService CreateHeapPageResourceProcessingService(HeapPageResourceConfiguration heapPageResourceConfiguration) =>
        new HeapPageResourceProcessingService(pages: new HeapPageResourceService(heapPageResourceConfiguration: heapPageResourceConfiguration));

    public int ReadHeapPageResourceSize() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return pages.ReadHeapPageResourceSize();
    });

    public ValueTask<byte[]> ReadHeapPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        return await pages.ReadHeapPageResourceAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WriteHeapPageResourceAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(pageId: pageId, source: source, cancellationToken: cancellationToken);

        await pages.WriteHeapPageResourceAsync(pageId: pageId, source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<ulong> AllocateHeapPageResourceAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        return await pages.AllocateHeapPageResourceAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask DiscardHeapPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        await pages.DiscardHeapPageResourceAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FreeHeapPageResourceAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        await pages.FreeHeapPageResourceAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<HeapPageLease> AcquireHeapPageLeaseAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        return await pages.AcquireHeapPageLeaseAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void MarkHeapPageLeaseDirty(HeapPageLease heapPageLease, ulong logSequenceNumber) =>
        TryCatch(operation: () =>
    {
        ValidateLeaseMark(heapPageLease: heapPageLease, logSequenceNumber: logSequenceNumber);
        pages.MarkHeapPageLeaseDirty(heapPageLease: heapPageLease, logSequenceNumber: logSequenceNumber);
    });

    public void ReleaseHeapPageLease(HeapPageLease heapPageLease) =>
        TryCatch(operation: () =>
    {
        ValidateLease(heapPageLease: heapPageLease);
        pages.ReleaseHeapPageLease(heapPageLease: heapPageLease);
    });

    public long ReadHeapPageResourceRandomSeed() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return pages.ReadHeapPageResourceRandomSeed();
    });

    public ulong? FindHeapPageResource(int requiredBytes) =>
        TryCatch(operation: () =>
    {
        ValidateRequiredBytes(requiredBytes: requiredBytes);
        return pages.FindHeapPageResource(requiredBytes: requiredBytes);
    });

    public void UpdateHeapPageResourceFreeSpace(ulong pageId, int freeBytes) =>
        TryCatch(operation: () =>
    {
        ValidateHeapPageResourceFreeSpaceOnUpdate(pageId: pageId, freeBytes: freeBytes);
        pages.UpdateHeapPageResourceFreeSpace(pageId: pageId, freeBytes: freeBytes);
    });

    public void RemoveHeapPageResourceFreeSpace(ulong pageId) =>
        TryCatch(operation: () =>
    {
        ValidateFreeSpacePage(pageId: pageId);
        pages.RemoveHeapPageResourceFreeSpace(pageId: pageId);
    });

    public void ClearHeapPageResourceFreeSpaces() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        pages.ClearHeapPageResourceFreeSpaces();
    });
}