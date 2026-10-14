// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.StorageMemoryResources;

namespace sql_storage_engine.Brokers.StorageMemoryResources;
internal interface IStorageMemoryResourceBroker
{
    Guid Create();

    ValueTask EnterAsync(Guid handle, CancellationToken cancellationToken);

    void Exit(Guid handle);

    void Close(Guid handle);
}