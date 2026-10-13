// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure
{
    public ValueTask ExecuteStatementAsync(Func<IStorageStatement, CancellationToken, ValueTask> operation, CancellationToken cancellationToken = default) =>
        new ValueTask(task: service.ExecuteStorageSessionContextEngineLifecycleAsync(storageSessionContext: new StorageSessionContext { Engine = engineContext, Execution = new StorageEngineExecutionContext { Operation = "Statement", CallbackSource = operation, OperationCallback = (context, token) => operation(arg1: new StorageSessionExposure(storageSessionContext: context), arg2: token) } }, cancellationToken: cancellationToken)
            .AsTask());
}