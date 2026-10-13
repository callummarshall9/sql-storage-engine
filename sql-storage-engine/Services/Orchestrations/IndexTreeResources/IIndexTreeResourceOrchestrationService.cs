// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Orchestrations.IndexTreeResources;
internal interface IIndexTreeResourceOrchestrationService
{
    int ReadIndexPageResourceSize();

    ValueTask<ulong> AllocateIndexPageResourceAsync(ushort pageType, CancellationToken cancellationToken);

    ValueTask<IndexPageLease> AcquireIndexPageLeaseAsync(ulong pageId, CancellationToken cancellationToken);

    void MarkIndexPageLeaseDirty(IndexPageLease indexPageLease, ulong logSequenceNumber);

    void ReleaseIndexPageLease(IndexPageLease indexPageLease);

    ulong ReadIndexRootResource();

    ValueTask UpdateIndexRootResourceAsync(ulong rootPageId, CancellationToken cancellationToken);
}