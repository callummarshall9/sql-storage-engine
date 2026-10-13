// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.TransactionRuntimes;

namespace sql_storage_engine.Brokers.TransactionRuntimes;
internal sealed class TransactionRuntimeBroker : ITransactionRuntimeBroker
{
    public object Create(Action notification) =>
        new TransactionRuntimeDependency(notification: notification);

    public CancellationToken ReadToken(object source) =>
        ((TransactionRuntimeDependency)source).ReadToken();

    public void Start(object source, TimeSpan timeout) =>
        ((TransactionRuntimeDependency)source).Start(timeout: timeout);

    public void Cancel(object source) =>
        ((TransactionRuntimeDependency)source).Cancel();

    public void Close(object source) =>
        ((TransactionRuntimeDependency)source).Dispose();

    public bool TryEnter(object source) =>
        ((TransactionRuntimeDependency)source).TryEnter();

    public Task WaitAsync(object source, CancellationToken token) =>
        ((TransactionRuntimeDependency)source).WaitAsync(token: token);

    public void Release(object source) =>
        ((TransactionRuntimeDependency)source).Release();

    public CancellationTokenSource Link(object source, CancellationToken token) =>
        CancellationTokenSource.CreateLinkedTokenSource(token1: ((TransactionRuntimeDependency)source).ReadToken(), token2: token);

    public CancellationToken ReadLinkedToken(CancellationTokenSource source) =>
        source.Token;

    public Task WaitTaskAsync(Task task, TimeSpan timeout, CancellationToken token) =>
        task.WaitAsync(timeout: timeout, cancellationToken: token);

    public Guid GenerateIdentity() =>
        Guid.NewGuid();
}