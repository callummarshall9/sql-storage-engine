// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.Coordination;
internal interface ITransactionCoordinationBroker
{
    Task WaitAsync(byte semaphoreId, CancellationToken cancellationToken);

    void Wait(byte semaphoreId);

    void Release(byte semaphoreId);

    void Close();
}