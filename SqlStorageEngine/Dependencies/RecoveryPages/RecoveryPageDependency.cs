// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.RecoveryPages;
internal sealed class RecoveryPageDependency : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async ValueTask ReadAsync(Func<ulong, Memory<byte>, CancellationToken, ValueTask> read, ulong pageId, Memory<byte> destination, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            await read(arg1: pageId, arg2: destination, arg3: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask WriteAsync(Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask> write, ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            await write(arg1: pageId, arg2: source, arg3: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            gate.Release();
        }
    }

    public int ReadPageSize(Func<int> pageSize) =>
        pageSize();

    public void Dispose() =>
        gate.Dispose();
}