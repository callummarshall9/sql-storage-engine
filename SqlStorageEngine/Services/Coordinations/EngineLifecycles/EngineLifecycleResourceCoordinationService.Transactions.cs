// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Coordinations.EngineLifecycles;
internal sealed partial class EngineLifecycleResourceCoordinationService
{
    public EngineResourceContext CreateTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.CreateTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext StartTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.StartTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext CancelTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.CancelTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext CloseTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.CloseTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext EnterTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.EnterTransactionOperationEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> WaitTransactionOperationEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await access.WaitTransactionOperationEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext ReleaseTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.ReleaseTransactionOperationEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext LinkTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.LinkTransactionTokenEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReadTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.ReadTransactionTokenEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> WaitTransactionTaskEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await access.WaitTransactionTaskEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }, task: engineResourceContext?.TaskSource);

    public EngineResourceContext GenerateTransactionIdentityEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.GenerateTransactionIdentityEngineResourceContext(engineResourceContext: engineResourceContext);
    });
}