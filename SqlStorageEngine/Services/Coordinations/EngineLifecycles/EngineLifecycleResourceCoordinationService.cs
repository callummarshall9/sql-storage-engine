// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Coordinations.EngineLifecycles;
internal sealed partial class EngineLifecycleResourceCoordinationService : IEngineLifecycleResourceCoordinationService
{
    private sql_storage_engine.Services.Orchestrations.BufferPools.BufferPoolContextOrchestrationService? buffers;
    private readonly sql_storage_engine.Services.Orchestrations.EngineAccesses.EngineAccessResourceOrchestrationService access;
    private EngineLifecycleResourceCoordinationService(sql_storage_engine.Services.Orchestrations.BufferPools.BufferPoolContextOrchestrationService buffers, sql_storage_engine.Services.Orchestrations.EngineAccesses.EngineAccessResourceOrchestrationService access)
    {
        this.buffers = buffers;
        this.access = access;
    }

    internal static EngineLifecycleResourceCoordinationService CreateEngineLifecycleResourceCoordinationService() =>
        new(buffers: null!, access: sql_storage_engine.Services.Orchestrations.EngineAccesses.EngineAccessResourceOrchestrationService.CreateEngineAccessResourceOrchestrationService());

    public EngineResourceContext AcquireEngineResourceContextFile(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.AcquireEngineResourceContextFile(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext CloseEngineResourceContextFile(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.CloseEngineResourceContextFile(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReadEngineResourceContextAccess(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.ReadEngineResourceContextAccess(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> EnterEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await access.EnterEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext EnterImmediateEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.EnterImmediateEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ActivateEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.ActivateEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext ReleaseEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.ReleaseEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public ValueTask<EngineResourceContext> DrainEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        return await access.DrainEngineResourceContextAsync(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public EngineResourceContext RegisterScopeEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        return access.RegisterScopeEngineResourceContext(engineResourceContext: engineResourceContext);
    });

    public EngineResourceContext OpenBufferEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);
        buffers = sql_storage_engine.Services.Orchestrations.BufferPools.BufferPoolContextOrchestrationService.CreateBufferPoolContextOrchestrationService(bufferPoolConfiguration: engineResourceContext.BufferConfiguration!);

        return engineResourceContext with
        {
            BufferSource = buffers
        };
    });

    public ValueTask<EngineResourceContext> FlushBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await buffers!.FlushBufferPoolContextAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> DiscardBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        await buffers!.DiscardBufferPoolContextAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return engineResourceContext;
    });

    public ValueTask<EngineResourceContext> CloseBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken) =>
        TryCatch<EngineResourceContext>(operation: async () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext, cancellationToken: cancellationToken);

        if (buffers is not null)
        {
            await buffers.CloseBufferPoolContextAsync(bufferPoolContext: null)
            .ConfigureAwait(continueOnCapturedContext: false);
        }

        return engineResourceContext;
    });

    public EngineResourceContext ReadProviderEngineResourceContext(EngineResourceContext engineResourceContext) =>
        TryCatch<EngineResourceContext>(operation: () =>
    {
        ValidateRequest(engineResourceContext: engineResourceContext);

        return engineResourceContext with
        {
            PinSource = async (id, token) =>
            {
                var page = await buffers!.ReadBufferPoolContextLeaseAsync(pageId: id, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);

                return (page.Lifetime!, page.ResultFrame!.Bytes!, page.PinDirty!);
            },
            DiscardSource = (id, token) => buffers!.DiscardBufferPoolContextPageAsync(pageId: id, cancellationToken: token),
            BufferFlushSource = token => buffers!.FlushBufferPoolContextAsync(cancellationToken: token)
        };
    });
}