// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Brokers.IndexTreeResources;

namespace sql_storage_engine.Services.Foundations.IndexTreeResources;
internal sealed partial class IndexPageResourceService : IIndexPageResourceService
{
    private readonly IndexPageResourceBroker broker;
    private readonly sql_storage_engine.Models.Indexes.IndexAllocationContext? allocations;
    public IndexPageResourceService(IndexTreeResourceConfiguration indexTreeResourceConfiguration)
    {
        broker = OpenIndexPageResourceBroker(indexTreeResourceConfiguration: indexTreeResourceConfiguration);
        allocations = indexTreeResourceConfiguration.Allocations;
    }

    private static IndexPageResourceBroker OpenIndexPageResourceBroker(IndexTreeResourceConfiguration indexTreeResourceConfiguration) =>
        TryCatch(operation: () =>
    {
        ValidateConfiguration(indexTreeResourceConfiguration: indexTreeResourceConfiguration);
        return new IndexPageResourceBroker(indexTreeResourceConfiguration: indexTreeResourceConfiguration);
    });

    public int ReadIndexPageResourceSize() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return broker.ReadPageSize();
    });

    public ValueTask<ulong> AllocateIndexPageResourceAsync(ushort pageType, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateAllocation(pageType: pageType, cancellationToken: cancellationToken);

        var pageId = await broker.AllocateAsync(pageType: pageType, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (allocations?.Tracking == true)
        {
            allocations.Pages!.Add(item: pageId);
        }

        return pageId;
    });

    public ValueTask<IndexPageLease> AcquireIndexPageLeaseAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        var acquired = await broker.AcquireAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return new IndexPageLease
        {
            Id = acquired.Id,
            Memory = acquired.Memory
        };
    });

    public void MarkIndexPageLeaseDirty(IndexPageLease indexPageLease, ulong logSequenceNumber) =>
        TryCatch(operation: () =>
    {
        ValidateLeaseMark(indexPageLease: indexPageLease, logSequenceNumber: logSequenceNumber);
        broker.MarkDirty(leaseId: indexPageLease.Id, logSequenceNumber: logSequenceNumber);
    });

    public void ReleaseIndexPageLease(IndexPageLease indexPageLease) =>
        TryCatch(operation: () =>
    {
        ValidateLease(indexPageLease: indexPageLease);
        broker.Release(leaseId: indexPageLease.Id);
    });
}