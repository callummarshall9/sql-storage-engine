// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Foundations.TransactionRuntimes;
internal sealed partial class TransactionRuntimeService : ITransactionRuntimeService
{
    private readonly sql_storage_engine.Brokers.TransactionRuntimes.TransactionRuntimeBroker broker = new();
    public EngineResourceContext CreateTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.ExpiryNotification);
        var source = broker.Create(notification: engineResourceContext.ExpiryNotification!);

        return engineResourceContext with
        {
            RuntimeSource = source,
            TokenResult = broker.ReadToken(source: source)
        };
    });

    public EngineResourceContext StartTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.RuntimeSource);
        broker.Start(source: engineResourceContext.RuntimeSource!, timeout: engineResourceContext.Timeout);
        return engineResourceContext;
    });

    public EngineResourceContext CancelTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.RuntimeSource);
        broker.Cancel(source: engineResourceContext.RuntimeSource!);
        return engineResourceContext;
    });

    public EngineResourceContext CloseTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.RuntimeSource);
        broker.Close(source: engineResourceContext.RuntimeSource!);
        return engineResourceContext;
    });

    public EngineResourceContext EnterTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.RuntimeSource);

        if (!broker.TryEnter(source: engineResourceContext.RuntimeSource!))
        {
            throw new InvalidOperationException(message: "A transaction operation is already active.");
        }

        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> WaitTransactionOperationEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.RuntimeSource);

        await broker.WaitAsync(source: engineResourceContext.RuntimeSource!, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    });

    public EngineResourceContext ReleaseTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.RuntimeSource);
        broker.Release(source: engineResourceContext.RuntimeSource!);
        return engineResourceContext;
    });

    public EngineResourceContext LinkTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.RuntimeSource);
        var link = broker.Link(source: engineResourceContext.RuntimeSource!, token: engineResourceContext.InputToken);

        return engineResourceContext with
        {
            TokenResult = broker.ReadLinkedToken(source: link),
            Lifetime = link
        };
    });

    public EngineResourceContext ReadTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.RuntimeSource);

        return engineResourceContext with
        {
            TokenResult = broker.ReadToken(source: engineResourceContext.RuntimeSource!)
        };
    });

    public ValueTask<EngineResourceContext> WaitTransactionTaskEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);
        ArgumentNullException.ThrowIfNull(argument: engineResourceContext.TaskSource);

        await broker.WaitTaskAsync(task: engineResourceContext.TaskSource!, timeout: engineResourceContext.Timeout, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    }, task: engineResourceContext?.TaskSource);

    public EngineResourceContext GenerateTransactionIdentityEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);

        return engineResourceContext with
        {
            DatabaseIdentity = broker.GenerateIdentity()
        };
    });
}