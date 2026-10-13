// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.TransactionRuntimes;
internal interface ITransactionRuntimeBroker
{
    object Create(Action notification);

    CancellationToken ReadToken(object source);

    void Start(object source, TimeSpan timeout);

    void Cancel(object source);

    void Close(object source);

    bool TryEnter(object source);

    Task WaitAsync(object source, CancellationToken token);

    void Release(object source);

    CancellationTokenSource Link(object source, CancellationToken token);

    CancellationToken ReadLinkedToken(CancellationTokenSource source);

    Task WaitTaskAsync(Task task, TimeSpan timeout, CancellationToken token);

    Guid GenerateIdentity();
}