// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.Access;
internal sealed class StorageGateDependency : IDisposable
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly AsyncLocal<object?> context = new();
    public Task WaitAsync(CancellationToken token) =>
        semaphore.WaitAsync(cancellationToken: token);

    public bool TryWait() =>
        semaphore.Wait(millisecondsTimeout: 0);

    public void Release() =>
        semaphore.Release();

    public object? ReadContext() =>
        context.Value;

    public void SetContext(object? value) =>
        context.Value = value;

    public void Dispose() =>
        semaphore.Dispose();
}