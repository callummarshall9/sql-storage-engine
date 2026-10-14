// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Orchestrations.EngineAccesses;
internal sealed partial class EngineAccessResourceOrchestrationService
{
    public EngineResourceContext CreateTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return runtime.CreateTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext StartTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return runtime.StartTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext CancelTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return runtime.CancelTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext CloseTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return runtime.CloseTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext EnterTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return runtime.EnterTransactionOperationEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> WaitTransactionOperationEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await runtime.WaitTransactionOperationEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext ReleaseTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return runtime.ReleaseTransactionOperationEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext LinkTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return runtime.LinkTransactionTokenEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReadTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return runtime.ReadTransactionTokenEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> WaitTransactionTaskEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await runtime.WaitTransactionTaskEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }, task: engineResourceContext?.TaskSource);

    public EngineResourceContext GenerateTransactionIdentityEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return runtime.GenerateTransactionIdentityEngineResourceContext(engineResourceContext: engineResourceContext);
    });
}