// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Managements.EngineResources;
internal sealed partial class EngineResourceContextManagementService
{
    public EngineResourceContext CreateTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.CreateTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext StartTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.StartTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext CancelTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.CancelTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext CloseTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.CloseTransactionRuntimeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext EnterTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.EnterTransactionOperationEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> WaitTransactionOperationEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await lifecycle.WaitTransactionOperationEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext ReleaseTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.ReleaseTransactionOperationEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext LinkTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.LinkTransactionTokenEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReadTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.ReadTransactionTokenEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> WaitTransactionTaskEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await lifecycle.WaitTransactionTaskEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    }, task: engineResourceContext?.TaskSource);

    public EngineResourceContext GenerateTransactionIdentityEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return lifecycle.GenerateTransactionIdentityEngineResourceContext(engineResourceContext: engineResourceContext);
    });
}