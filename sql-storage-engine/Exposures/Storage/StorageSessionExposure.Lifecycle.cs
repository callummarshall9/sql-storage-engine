// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure
{
    public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextEngineLifecycleAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Execution = new StorageEngineExecutionContext { Operation = "Flush" } }, cancellationToken: cancellationToken)
            .AsTask());

    public ValueTask DisposeAsync() =>
        new ValueTask(task: service.ExecuteStorageSessionContextEngineLifecycleAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Transaction = sessionContext.Transaction, Execution = new StorageEngineExecutionContext { Operation = "Dispose" } }, cancellationToken: CancellationToken.None)
            .AsTask());
}