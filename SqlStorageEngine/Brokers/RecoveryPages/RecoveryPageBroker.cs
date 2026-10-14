// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.RecoveryPages;

namespace sql_storage_engine.Brokers.RecoveryPages;
internal sealed class RecoveryPageBroker : IRecoveryPageBroker
{
    private readonly RecoveryPageDependency dependency = new();
    public ValueTask ReadAsync(Func<ulong, Memory<byte>, CancellationToken, ValueTask> read, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        dependency.ReadAsync(read: read, pageId: pageId, destination: destination, cancellationToken: cancellationToken);

    public ValueTask WriteAsync(Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        dependency.WriteAsync(write: write, pageId: pageId, source: source, cancellationToken: cancellationToken);

    public int ReadPageSize(Func<int> pageSize) =>
        dependency.ReadPageSize(pageSize: pageSize);
}