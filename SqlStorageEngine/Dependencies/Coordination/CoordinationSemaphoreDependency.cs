// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.Coordination;
internal sealed class CoordinationSemaphoreDependency : IDisposable
{
    private readonly SemaphoreSlim[] semaphores = [new SemaphoreSlim(1, 1), new SemaphoreSlim(1, 1), new SemaphoreSlim(1, 1)];
    public Task WaitAsync(byte semaphoreId, CancellationToken cancellationToken) =>
        semaphores[semaphoreId].WaitAsync(cancellationToken: cancellationToken);

    public void Wait(byte semaphoreId) =>
        semaphores[semaphoreId].Wait();

    public void Release(byte semaphoreId) =>
        semaphores[semaphoreId].Release();

    public void Dispose()
    {
        foreach (var semaphore in semaphores)
        {
            semaphore.Dispose();
        }
    }
}