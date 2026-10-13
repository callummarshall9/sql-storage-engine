// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Buffers;
using sql_storage_engine.Dependencies.BufferEnvironments;

namespace sql_storage_engine.Brokers.BufferEnvironments;
internal sealed class BufferPoolEnvironmentBroker : IBufferPoolEnvironmentBroker
{
    private readonly BufferPoolEnvironmentDependency dependency;
    public BufferPoolEnvironmentBroker(BufferPoolConfiguration bufferPoolConfiguration)
    {
        dependency = new BufferPoolEnvironmentDependency(pageSize: bufferPoolConfiguration.PageSize!, read: bufferPoolConfiguration.Read!, write: bufferPoolConfiguration.Write!, flush: bufferPoolConfiguration.Flush!, close: bufferPoolConfiguration.Close!, guard: bufferPoolConfiguration.Guard, leaveOpen: bufferPoolConfiguration.LeaveOpen, guardPresent: bufferPoolConfiguration.GuardPresent);
    }

    public int ReadPageSize() =>
        dependency.ReadPageSize();

    public void Enter() =>
        dependency.Enter();

    public ValueTask EnterAsync(CancellationToken cancellationToken) =>
        dependency.EnterAsync(cancellationToken: cancellationToken);

    public void Exit() =>
        dependency.Exit();

    public void RecordHit() =>
        dependency.RecordHit();

    public void RecordMiss() =>
        dependency.RecordMiss();

    public ValueTask ReadAsync(ulong pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        dependency.ReadAsync(pageId: pageId, destination: destination, cancellationToken: cancellationToken);

    public ValueTask WriteAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        dependency.WriteAsync(pageId: pageId, source: source, cancellationToken: cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        dependency.FlushAsync(cancellationToken: cancellationToken);

    public ValueTask GuardAsync(ulong pageId, ulong logSequenceNumber, CancellationToken cancellationToken) =>
        dependency.GuardAsync(pageId: pageId, logSequenceNumber: logSequenceNumber, cancellationToken: cancellationToken);

    public ValueTask CloseAsync() =>
        dependency.DisposeAsync();

    public IDisposable CreatePinLifetime(Action release) =>
        System.Reactive.Disposables.Disposable.Create(dispose: release);
}