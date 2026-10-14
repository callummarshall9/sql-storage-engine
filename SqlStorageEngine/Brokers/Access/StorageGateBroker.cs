// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.Access;

namespace sql_storage_engine.Brokers.Access;
internal sealed class StorageGateBroker : IStorageGateBroker
{
    private readonly StorageGateDependency dependency = new();
    public Task WaitAsync(CancellationToken token) =>
        dependency.WaitAsync(token: token);

    public bool TryWait() =>
        dependency.TryWait();

    public void Release() =>
        dependency.Release();

    public object? ReadContext() =>
        dependency.ReadContext();

    public void SetContext(object? value) =>
        dependency.SetContext(value: value);

    public TaskCompletionSource CreateCompletion() =>
        new TaskCompletionSource(creationOptions: TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ReadCompletion(TaskCompletionSource completion) =>
        completion.Task;

    public void Complete(TaskCompletionSource completion) =>
        completion.TrySetResult();

    public ValueTask DisposeResourceAsync(IAsyncDisposable resource) =>
        resource.DisposeAsync();

    public void Close() =>
        dependency.Dispose();

    public IDisposable CreateLifetime(Action release) =>
        System.Reactive.Disposables.Disposable.Create(dispose: release);
}