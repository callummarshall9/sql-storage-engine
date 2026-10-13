// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.CatalogPages;
internal sealed class CatalogPageResourceDependency(Func<int> pageSize, Func<ushort, CancellationToken, ValueTask<ulong>> allocate, Func<ulong, CancellationToken, ValueTask> free, Func<ulong, Memory<byte>, CancellationToken, ValueTask> read, Func<ulong, ReadOnlyMemory<byte>, CancellationToken, ValueTask> write) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public int ReadPageSize() =>
        pageSize();

    public ValueTask<ulong> AllocateAsync(ushort pageType, CancellationToken token) =>
        allocate(arg1: pageType, arg2: token);

    public ValueTask FreeAsync(ulong pageId, CancellationToken token) =>
        free(arg1: pageId, arg2: token);

    public async ValueTask ReadAsync(ulong pageId, Memory<byte> bytes, CancellationToken token)
    {
        await gate.WaitAsync(cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            await read(arg1: pageId, arg2: bytes, arg3: token)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask WriteAsync(ulong pageId, ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        await gate.WaitAsync(cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            await write(arg1: pageId, arg2: bytes, arg3: token)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() =>
        gate.Dispose();
}