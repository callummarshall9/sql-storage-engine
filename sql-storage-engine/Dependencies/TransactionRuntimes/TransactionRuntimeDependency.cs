// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Dependencies.TransactionRuntimes;
internal sealed class TransactionRuntimeDependency : IDisposable
{
    private readonly CancellationTokenSource deadline = new();
    private readonly SemaphoreSlim operation = new(1, 1);
    private readonly CancellationTokenRegistration expiry;
    public TransactionRuntimeDependency(Action notification)
    {
        expiry = deadline.Token.UnsafeRegister(callback: _ => notification(), state: null);
    }

    public CancellationToken ReadToken() =>
        deadline.Token;

    public void Start(TimeSpan timeout) =>
        deadline.CancelAfter(delay: timeout);

    public void Cancel() =>
        deadline.Cancel();

    public bool TryEnter() =>
        operation.Wait(millisecondsTimeout: 0);

    public Task WaitAsync(CancellationToken token) =>
        operation.WaitAsync(cancellationToken: token);

    public void Release() =>
        operation.Release();

    public void Dispose()
    {
        expiry.Dispose();
        deadline.Dispose();
        operation.Dispose();
    }
}