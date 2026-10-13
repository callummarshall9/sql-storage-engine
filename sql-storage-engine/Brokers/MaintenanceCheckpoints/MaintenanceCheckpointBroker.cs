// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.MaintenanceCheckpoints;

namespace sql_storage_engine.Brokers.MaintenanceCheckpoints;
internal sealed class MaintenanceCheckpointBroker : IMaintenanceCheckpointBroker
{
    private readonly MaintenanceCheckpointDependency resource = new MaintenanceCheckpointDependency();
    public ValueTask<T> ReadAsync<T>(Func<CancellationToken, ValueTask<T>> read, CancellationToken cancellationToken) =>
        resource.ReadAsync(read: read, cancellationToken: cancellationToken);

    public ValueTask WriteAsync<T>(Func<T, CancellationToken, ValueTask> write, T value, CancellationToken cancellationToken) =>
        resource.WriteAsync(write: write, value: value, cancellationToken: cancellationToken);

    public ValueTask YieldAsync(Func<CancellationToken, ValueTask> throttle, CancellationToken cancellationToken) =>
        resource.YieldAsync(throttle: throttle, cancellationToken: cancellationToken);

    public void Close() =>
        resource.Dispose();
}