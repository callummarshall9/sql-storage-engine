// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Dependencies.StorageMemoryResources;

namespace sql_storage_engine.Brokers.StorageMemoryResources;
internal sealed class StorageMemoryResourceBroker : IStorageMemoryResourceBroker
{
    private readonly StorageMemoryResourceDependency resource = new StorageMemoryResourceDependency();

    public Guid Create() =>
        resource.Create();

    public ValueTask EnterAsync(Guid handle, CancellationToken cancellationToken) =>
        resource.EnterAsync(handle: handle, cancellationToken: cancellationToken);

    public void Exit(Guid handle) =>
        resource.Exit(handle: handle);

    public void Close(Guid handle) =>
        resource.Close(handle: handle);
}