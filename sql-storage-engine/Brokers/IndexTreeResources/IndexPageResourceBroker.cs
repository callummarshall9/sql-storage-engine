// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Dependencies.IndexTreeResources;

namespace sql_storage_engine.Brokers.IndexTreeResources;
internal sealed class IndexPageResourceBroker : IIndexPageResourceBroker
{
    private readonly IndexPageResourceDependency dependency;
    public IndexPageResourceBroker(IndexTreeResourceConfiguration indexTreeResourceConfiguration)
    {
        dependency = new IndexPageResourceDependency(pageSize: indexTreeResourceConfiguration.PageSize!, pin: indexTreeResourceConfiguration.Pin!, allocate: indexTreeResourceConfiguration.Allocate!);
    }

    public int ReadPageSize() =>
        dependency.ReadPageSize();

    public ValueTask<ulong> AllocateAsync(ushort pageType, CancellationToken cancellationToken) =>
        dependency.AllocateAsync(pageType: pageType, cancellationToken: cancellationToken);

    public ValueTask<(Guid Id, Memory<byte> Memory)> AcquireAsync(ulong pageId, CancellationToken cancellationToken) =>
        dependency.AcquireAsync(pageId: pageId, cancellationToken: cancellationToken);

    public void MarkDirty(Guid leaseId, ulong logSequenceNumber) =>
        dependency.MarkDirty(leaseId: leaseId, logSequenceNumber: logSequenceNumber);

    public void Release(Guid leaseId) =>
        dependency.Release(leaseId: leaseId);
}