// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Dependencies.OverflowPages;

namespace sql_storage_engine.Brokers.OverflowPages;
internal sealed class OverflowPageResourceBroker : IOverflowPageResourceBroker
{
    private readonly OverflowPageResourceDependency dependency;
    public OverflowPageResourceBroker(OverflowPageResourceConfiguration overflowPageResourceConfiguration)
    {
        dependency = new OverflowPageResourceDependency(pageSize: overflowPageResourceConfiguration.PageSize!, pin: overflowPageResourceConfiguration.Pin!, allocate: overflowPageResourceConfiguration.Allocate!, discard: overflowPageResourceConfiguration.Discard!, free: overflowPageResourceConfiguration.Free!);
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
}