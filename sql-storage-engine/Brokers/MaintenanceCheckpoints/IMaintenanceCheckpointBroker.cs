// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.MaintenanceCheckpoints;
internal interface IMaintenanceCheckpointBroker
{
    ValueTask<T> ReadAsync<T>(Func<CancellationToken, ValueTask<T>> read, CancellationToken cancellationToken);

    ValueTask WriteAsync<T>(Func<T, CancellationToken, ValueTask> write, T value, CancellationToken cancellationToken);

    ValueTask YieldAsync(Func<CancellationToken, ValueTask> throttle, CancellationToken cancellationToken);

    void Close();
}