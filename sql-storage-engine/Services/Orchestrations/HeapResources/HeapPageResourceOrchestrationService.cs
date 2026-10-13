// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Heap;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Services.Processings.HeapResources;
using sql_storage_engine.Services.Processings.FreeSpaces;

namespace sql_storage_engine.Services.Orchestrations.HeapResources;
internal sealed partial class HeapPageResourceOrchestrationService : IHeapPageResourceOrchestrationService
{
    private readonly HeapPageResourceProcessingService pages;
    private readonly FreeSpacePageIdProcessingService spaces;
    private readonly bool customSpaces;
    private HeapPageResourceOrchestrationService(HeapPageResourceProcessingService pages, FreeSpacePageIdProcessingService spaces, HeapPageResourceConfiguration heapPageResourceConfiguration)
    {
        this.pages = pages;
        this.spaces = spaces;
        customSpaces = heapPageResourceConfiguration.FreeSpaceSource is not null;
    }

    internal static HeapPageResourceOrchestrationService CreateHeapPageResourceOrchestrationService(HeapPageResourceConfiguration heapPageResourceConfiguration)
    {
        var pages = HeapPageResourceProcessingService.CreateHeapPageResourceProcessingService(heapPageResourceConfiguration: heapPageResourceConfiguration);
        return new HeapPageResourceOrchestrationService(pages: pages, spaces: new FreeSpacePageIdProcessingService(configuration: new FreeSpaceConfiguration { PageSize = pages.ReadHeapPageResourceSize() }), heapPageResourceConfiguration: heapPageResourceConfiguration);
    }

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

        if (customSpaces)
        {
            return pages.FindHeapPageResource(requiredBytes: requiredBytes);
        }

        return spaces.FindFreeSpacePageId(requiredBytes: requiredBytes)?.Value;
    });

    public void UpdateHeapPageResourceFreeSpace(ulong pageId, int freeBytes) =>
        TryCatch(operation: () =>
    {
        ValidateHeapPageResourceFreeSpaceOnUpdate(pageId: pageId, freeBytes: freeBytes);

        if (customSpaces)
        {
            pages.UpdateHeapPageResourceFreeSpace(pageId: pageId, freeBytes: freeBytes);
        }
        else
        {
            spaces.UpdateFreeSpacePageId(pageId: pageId, freeBytes: freeBytes);
        }
    });

    public void RemoveHeapPageResourceFreeSpace(ulong pageId) =>
        TryCatch(operation: () =>
    {
        ValidateFreeSpacePage(pageId: pageId);

        if (customSpaces)
        {
            pages.RemoveHeapPageResourceFreeSpace(pageId: pageId);
        }
        else
        {
            spaces.RemoveFreeSpacePageId(pageId: pageId);
        }
    });

    public void ClearHeapPageResourceFreeSpaces() =>
        TryCatch(operation: () =>
    {
        ValidateResource();

        if (customSpaces)
        {
            pages.ClearHeapPageResourceFreeSpaces();
        }
        else
        {
            spaces.ClearFreeSpacePageIds();
        }
    });

    public int ReadHeapPageResourceSize() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return pages.ReadHeapPageResourceSize();
    });

    public ValueTask<ulong> AllocateHeapPageResourceAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCancellation(cancellationToken: cancellationToken);

        return await pages.AllocateHeapPageResourceAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}