// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.MaintenanceCheckpoints;
internal sealed class MaintenanceCheckpointDependency : IDisposable
{
    private readonly SemaphoreSlim gate = new(initialCount: 1, maxCount: 1);
    public async ValueTask<T> ReadAsync<T>(Func<CancellationToken, ValueTask<T>> read, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            return await read(arg: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask WriteAsync<T>(Func<T, CancellationToken, ValueTask> write, T value, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            await write(arg1: value, arg2: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask YieldAsync(Func<CancellationToken, ValueTask> throttle, CancellationToken cancellationToken)
    {
        await throttle(arg: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public void Dispose() =>
        gate.Dispose();
}