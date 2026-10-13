// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.Coordination;

namespace sql_storage_engine.Brokers.Coordination;
internal sealed class TransactionCoordinationBroker : ITransactionCoordinationBroker
{
    private readonly CoordinationSemaphoreDependency dependency = new();
    public Task WaitAsync(byte semaphoreId, CancellationToken cancellationToken) =>
        dependency.WaitAsync(semaphoreId: semaphoreId, cancellationToken: cancellationToken);

    public void Wait(byte semaphoreId) =>
        dependency.Wait(semaphoreId: semaphoreId);

    public void Release(byte semaphoreId) =>
        dependency.Release(semaphoreId: semaphoreId);

    public void Close() =>
        dependency.Dispose();
}