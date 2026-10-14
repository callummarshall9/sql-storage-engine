// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Coordinations.EngineLifecycles;
internal interface IEngineLifecycleResourceCoordinationService
{
    EngineResourceContext AcquireEngineResourceContextFile(EngineResourceContext engineResourceContext);

    EngineResourceContext CloseEngineResourceContextFile(EngineResourceContext engineResourceContext);

    EngineResourceContext ReadEngineResourceContextAccess(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> EnterEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext EnterImmediateEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext ActivateEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext ReleaseEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> DrainEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext RegisterScopeEngineResourceContext(EngineResourceContext engineResourceContext);



    EngineResourceContext OpenBufferEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> FlushBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> DiscardBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    ValueTask<EngineResourceContext> CloseBufferEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext TrackEngineResourceContextStream<T>(EngineResourceContext engineResourceContext);

    EngineResourceContext ReadProviderEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext CreateTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext StartTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext CancelTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext CloseTransactionRuntimeEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext EnterTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> WaitTransactionOperationEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext ReleaseTransactionOperationEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext LinkTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext);

    EngineResourceContext ReadTransactionTokenEngineResourceContext(EngineResourceContext engineResourceContext);

    ValueTask<EngineResourceContext> WaitTransactionTaskEngineResourceContextAsync(EngineResourceContext engineResourceContext, CancellationToken cancellationToken);

    EngineResourceContext GenerateTransactionIdentityEngineResourceContext(EngineResourceContext engineResourceContext);
}