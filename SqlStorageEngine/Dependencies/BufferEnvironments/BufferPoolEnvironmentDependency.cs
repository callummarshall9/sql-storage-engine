// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.BufferEnvironments;
internal sealed class BufferPoolEnvironmentDependency : IAsyncDisposable
{
    private readonly Func<int> pageSize;
    private readonly Func<ulong, Memory<byte>, CancellationToken, ValueTask> read;
    private readonly Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask> write;
    private readonly Func<CancellationToken, ValueTask> flush;
    private readonly Func<ValueTask> close;
    private readonly Func<ulong, ulong, CancellationToken, ValueTask>? guard;
    private readonly bool leaveOpen;
    private readonly bool guardPresent;
    private readonly SemaphoreSlim gate = new(initialCount: 1, maxCount: 1);
    private readonly Meter meter = new("sql-storage-engine.buffer-pool");
    private readonly Counter<long> hit;
    private readonly Counter<long> miss;
    private bool disposed;
    public BufferPoolEnvironmentDependency(Func<int> pageSize, Func<ulong, Memory<byte>, CancellationToken, ValueTask> read, Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, Func<CancellationToken, ValueTask> flush, Func<ValueTask> close, Func<ulong, ulong, CancellationToken, ValueTask>? guard, bool leaveOpen, bool guardPresent)
    {
        this.pageSize = pageSize;
        this.read = read;
        this.write = write;
        this.flush = flush;
        this.close = close;
        this.guard = guard;
        this.leaveOpen = leaveOpen;
        this.guardPresent = guardPresent;
        hit = meter.CreateCounter<long>(name: "buffer_pool.hits");
        miss = meter.CreateCounter<long>(name: "buffer_pool.misses");
    }

    public int ReadPageSize() =>
        pageSize();

    public void Enter() =>
        gate.Wait();

    public ValueTask EnterAsync(CancellationToken cancellationToken) =>
        new(gate.WaitAsync(cancellationToken: cancellationToken));

    public void Exit() =>
        gate.Release();

    public void RecordHit() =>
        hit.Add(delta: 1);

    public void RecordMiss() =>
        miss.Add(delta: 1);

    public ValueTask ReadAsync(ulong pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        read(arg1: pageId, arg2: destination, arg3: cancellationToken);

    public ValueTask WriteAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        write(arg1: pageId, arg2: source, arg3: cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken) =>
        flush(arg: cancellationToken);

    public ValueTask GuardAsync(ulong pageId, ulong logSequenceNumber, CancellationToken cancellationToken) =>
        !guardPresent ? ValueTask.CompletedTask : guard!(arg1: pageId, arg2: logSequenceNumber, arg3: cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        try
        {
            if (!leaveOpen)
            {
                await close()
            .ConfigureAwait(continueOnCapturedContext: false);
            }
        }
        finally
        {
            gate.Dispose();
            meter.Dispose();
        }
    }
}