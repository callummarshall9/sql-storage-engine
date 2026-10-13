// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Foundations.TransactionRuntimes;
internal interface ITransactionRuntimeService
{
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