// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.IndexTreeResources;
internal sealed class IndexRootResourceDependency(Func<ulong> read, Func<ulong, CancellationToken, ValueTask> update) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public ulong Read() =>
        read();

    public async ValueTask UpdateAsync(ulong rootPageId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            await update(arg1: rootPageId, arg2: cancellationToken)
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