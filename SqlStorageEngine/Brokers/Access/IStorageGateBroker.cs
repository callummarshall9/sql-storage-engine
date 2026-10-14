// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.Access;
internal interface IStorageGateBroker
{
    Task WaitAsync(CancellationToken token);

    bool TryWait();

    void Release();

    object? ReadContext();

    void SetContext(object? value);

    TaskCompletionSource CreateCompletion();

    Task ReadCompletion(TaskCompletionSource completion);

    void Complete(TaskCompletionSource completion);

    ValueTask DisposeResourceAsync(IAsyncDisposable resource);

    void Close();

    IDisposable CreateLifetime(Action release);
}