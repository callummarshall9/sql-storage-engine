// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Services.Foundations.IndexTreeResources;

namespace sql_storage_engine.Services.Orchestrations.IndexTreeResources;
internal sealed partial class IndexTreeResourceOrchestrationService : IIndexTreeResourceOrchestrationService
{
    private readonly IndexPageResourceService pages;
    private readonly IndexRootResourceService roots;
    public IndexTreeResourceOrchestrationService(IndexTreeResourceConfiguration indexTreeResourceConfiguration)
    {
        pages = new IndexPageResourceService(indexTreeResourceConfiguration: indexTreeResourceConfiguration);
        roots = new IndexRootResourceService(indexTreeResourceConfiguration: indexTreeResourceConfiguration);
    }

    public int ReadIndexPageResourceSize() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return pages.ReadIndexPageResourceSize();
    });

    public ValueTask<ulong> AllocateIndexPageResourceAsync(ushort pageType, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateAllocation(pageType: pageType, cancellationToken: cancellationToken);

        return await pages.AllocateIndexPageResourceAsync(pageType: pageType, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask<IndexPageLease> AcquireIndexPageLeaseAsync(ulong pageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePage(pageId: pageId, cancellationToken: cancellationToken);

        return await pages.AcquireIndexPageLeaseAsync(pageId: pageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void MarkIndexPageLeaseDirty(IndexPageLease indexPageLease, ulong logSequenceNumber) =>
        TryCatch(operation: () =>
    {
        ValidateLeaseMark(indexPageLease: indexPageLease, logSequenceNumber: logSequenceNumber);
        pages.MarkIndexPageLeaseDirty(indexPageLease: indexPageLease, logSequenceNumber: logSequenceNumber);
    });

    public void ReleaseIndexPageLease(IndexPageLease indexPageLease) =>
        TryCatch(operation: () =>
    {
        ValidateLease(indexPageLease: indexPageLease);
        pages.ReleaseIndexPageLease(indexPageLease: indexPageLease);
    });

    public ulong ReadIndexRootResource() =>
        TryCatch(operation: () =>
    {
        ValidateResource();
        return roots.ReadIndexRootResource();
    });

    public ValueTask UpdateIndexRootResourceAsync(ulong rootPageId, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIndexRootResourceOnUpdate(rootPageId: rootPageId, cancellationToken: cancellationToken);

        await roots.UpdateIndexRootResourceAsync(rootPageId: rootPageId, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}